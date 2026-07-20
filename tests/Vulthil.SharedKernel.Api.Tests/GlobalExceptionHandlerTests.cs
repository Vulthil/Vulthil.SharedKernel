using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Api.Tests;

public sealed class GlobalExceptionHandlerTests : BaseUnitTestCase
{
    private readonly Lazy<GlobalExceptionHandler> _lazyTarget;
    private GlobalExceptionHandler Target => _lazyTarget.Value;

    public GlobalExceptionHandlerTests()
    {
        _lazyTarget = new(CreateInstance<GlobalExceptionHandler>);
    }

    [Fact]
    public async Task TryHandleAsyncWritesFiveHundredProblemDetailsWithoutLeakingTheExceptionMessage()
    {
        // Arrange
        ProblemDetailsContext? capturedContext = null;
        GetMock<IProblemDetailsService>()
            .Setup(service => service.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(context => capturedContext = context)
            .ReturnsAsync(true);
        var httpContext = new DefaultHttpContext();
        var exception = new InvalidOperationException("sensitive internal detail");

        // Act
        var handled = await Target.TryHandleAsync(httpContext, exception, CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.NotNull(capturedContext);
        Assert.Same(exception, capturedContext.Exception);
        Assert.Same(httpContext, capturedContext.HttpContext);
        Assert.Equal("An unexpected error occurred", capturedContext.ProblemDetails.Title);
        Assert.Equal(StatusCodes.Status500InternalServerError, capturedContext.ProblemDetails.Status);
        Assert.Null(capturedContext.ProblemDetails.Detail);
    }

    [Fact]
    public async Task TryHandleAsyncReturnsFalseWhenTheProblemDetailsServiceCannotWriteTheResponse()
    {
        // Arrange
        GetMock<IProblemDetailsService>()
            .Setup(service => service.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .ReturnsAsync(false);
        var httpContext = new DefaultHttpContext();

        // Act
        var handled = await Target.TryHandleAsync(httpContext, new InvalidOperationException(), CancellationToken);

        // Assert
        Assert.False(handled);
    }

    [Fact]
    public async Task TryHandleAsyncThrowsOnNullHttpContext()
    {
        // Arrange

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await Target.TryHandleAsync(null!, new InvalidOperationException(), CancellationToken));
    }
}
