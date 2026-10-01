using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vulthil.Results;
using Vulthil.SharedKernel.Application;
using Vulthil.SharedKernel.Exceptions;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Api.Tests;

/// <summary>
/// Sends real requests through a test server, so the problem bodies a client receives are pinned: a controller and a
/// minimal API endpoint answer the same error with the same body, and an exception that carries an error is answered
/// like a failed result with that error.
/// </summary>
public sealed class ProblemResponseTests : BaseUnitTestCase
{
    private const string UnexpectedMessage = "sensitive internal detail";

    internal static readonly Error WidgetNotFound = Error.NotFound("Widget.NotFound", "Widget 1 was not found.");
    private static readonly Error WidgetGone = Error.Conflict("Widget.Gone", "Widget 1 is gone.");
    internal static readonly ValidationError InvalidWidget = new([
        Error.Validation("Name", "Name is required."),
        Error.Validation("Size", "Size must be positive."),
    ]);
    private static readonly ValidationFailure NameMissing = new("Name", "Name is required.") { ErrorCode = "NotEmptyValidator" };

    private readonly CapturingLoggerProvider _logs = new();
    private WebApplication? _app;
    private HttpClient? _client;

    private HttpClient Client => _client ?? throw new InvalidOperationException("The test server is not started.");

    protected override async ValueTask Initialize()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders().AddProvider(_logs);
        builder.Services.AddProblemDetailsHandling();
        builder.Services.AddControllers().AddApplicationPart(typeof(ProblemResponseTests).Assembly);

        _app = builder.Build();
        _app.UseProblemDetailsHandling();
        _app.MapControllers();
        _app.MapGet("/minimal/not-found", () => WidgetNotFound.ToIResult());
        _app.MapGet("/minimal/gone", () => WidgetGone.ToIResult());
        _app.MapGet("/minimal/validation", () => InvalidWidget.ToIResult());
        _app.MapGet("/minimal/name-missing", () => Result.Failure<string>(new ValidationError([Error.Validation(NameMissing.ErrorCode, NameMissing.ErrorMessage)])).ToIResult());
        _app.MapGet("/throws/domain", IResult () => throw new WidgetGoneException());
        _app.MapGet("/throws/validation", IResult () => throw new CommandValidationException([NameMissing]));
        _app.MapGet("/throws/unexpected", IResult () => throw new InvalidOperationException(UnexpectedMessage));

        await _app.StartAsync(CancellationToken);
        _client = _app.GetTestClient();
    }

    protected override async ValueTask Dispose()
    {
        _client?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        _logs.Dispose();
        await base.Dispose();
    }

    [Theory]
    [InlineData("not-found")]
    [InlineData("validation")]
    public async Task AControllerAndAMinimalApiEndpointAnswerTheSameErrorWithTheSameBody(string error)
    {
        // Act
        var minimal = await GetProblemAsync($"/minimal/{error}");
        var controller = await GetProblemAsync($"/mvc/{error}");

        // Assert
        Assert.Equal(minimal.Status, controller.Status);
        Assert.True(JsonNode.DeepEquals(minimal.Body, controller.Body), $"minimal: {minimal.Body}{Environment.NewLine}controller: {controller.Body}");
    }

    [Fact]
    public async Task AControllerAnswersAnErrorWithTheCompleteProblemBody()
    {
        // Act
        var problem = await GetProblemAsync("/mvc/not-found");

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        Assert.Equal("application/problem+json", problem.ContentType);
        Assert.Equal("Not Found", (string?)problem.Body["title"]);
        Assert.NotNull(problem.Body["type"]);
        Assert.Equal(WidgetNotFound.Description, (string?)problem.Body["detail"]);
        Assert.NotNull(problem.Body[WidgetNotFound.Code]);
        Assert.Equal("GET /mvc/not-found", problem.Instance);
        Assert.False(string.IsNullOrEmpty(problem.RequestId));
    }

    [Fact]
    public async Task AControllerAnswersAValidationErrorWithItsDetail()
    {
        // Act
        var problem = await GetProblemAsync("/mvc/validation");

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal(InvalidWidget.Description, (string?)problem.Body["detail"]);
        Assert.NotNull(problem.Body["errors"]?["Name"]);
        Assert.NotNull(problem.Body["errors"]?["Size"]);
    }

    [Fact]
    public async Task ADomainExceptionIsAnsweredLikeAFailedResultWithItsError()
    {
        // Act
        var thrown = await GetProblemAsync("/throws/domain");
        var returned = await GetProblemAsync("/minimal/gone");

        // Assert
        Assert.Equal(StatusCodes.Status409Conflict, thrown.Status);
        Assert.True(JsonNode.DeepEquals(returned.Body, thrown.Body), $"returned: {returned.Body}{Environment.NewLine}thrown: {thrown.Body}");
    }

    [Fact]
    public async Task ACommandValidationExceptionIsAnsweredWithTheValidationProblemOfAFailedResult()
    {
        // Act
        var thrown = await GetProblemAsync("/throws/validation");
        var returned = await GetProblemAsync("/minimal/name-missing");

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, thrown.Status);
        Assert.True(JsonNode.DeepEquals(returned.Body, thrown.Body), $"returned: {returned.Body}{Environment.NewLine}thrown: {thrown.Body}");
    }

    [Fact]
    public async Task AnyOtherExceptionIsAnsweredWithTheGenericServerErrorWithoutItsMessage()
    {
        // Act
        var problem = await GetProblemAsync("/throws/unexpected");

        // Assert
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.Status);
        Assert.Equal("An unexpected error occurred", (string?)problem.Body["title"]);
        Assert.DoesNotContain(UnexpectedMessage, problem.Body.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnExceptionAnsweredWithAClientErrorIsLoggedAsAWarning()
    {
        // Act
        await GetProblemAsync("/throws/domain");

        // Assert
        var entry = Assert.Single(_logs.EntriesFor<GlobalExceptionHandler>());
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.IsType<WidgetGoneException>(entry.Exception);
    }

    [Fact]
    public async Task AnUnexpectedExceptionIsLoggedAsAnError()
    {
        // Act
        await GetProblemAsync("/throws/unexpected");

        // Assert
        var entry = Assert.Single(_logs.EntriesFor<GlobalExceptionHandler>());
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    private async Task<CapturedProblem> GetProblemAsync(string path)
    {
        using var response = await Client.GetAsync(new Uri(path, UriKind.Relative), CancellationToken);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(CancellationToken))!.AsObject();

        var instance = (string?)body["instance"];
        var requestId = (string?)body["requestId"];
        body.Remove("instance");
        body.Remove("requestId");
        body.Remove("traceId");

        return new CapturedProblem((int)response.StatusCode, response.Content.Headers.ContentType?.MediaType, body, instance, requestId);
    }

    private sealed record CapturedProblem(int Status, string? ContentType, JsonObject Body, string? Instance, string? RequestId);

    public sealed class WidgetGoneException() : DomainException(WidgetGone);

    private sealed record CapturedLog(string Category, LogLevel Level, Exception? Exception);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<CapturedLog> _entries = new();

        public IEnumerable<CapturedLog> EntriesFor<TCategory>()
            => _entries.Where(entry => entry.Category == typeof(TCategory).FullName);

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLog> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => entries.Enqueue(new CapturedLog(category, logLevel, exception));
        }
    }
}

/// <summary>
/// The controller side of <see cref="ProblemResponseTests"/>. MVC discovers only top-level public controllers, so it
/// cannot be nested in the test class.
/// </summary>
[ApiController]
[Route("mvc")]
public sealed class ProblemResponseTestsController : ControllerBase
{
    [HttpGet("not-found")]
    public IActionResult WidgetIsMissing() => ProblemResponseTests.WidgetNotFound.ToActionResult(this);

    [HttpGet("validation")]
    public IActionResult WidgetIsInvalid() => ProblemResponseTests.InvalidWidget.ToActionResult(this);
}
