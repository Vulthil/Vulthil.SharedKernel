using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Api.Tests;

public sealed class ProblemDetailsExtensionsTests : BaseUnitTestCase
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddProblemDetailsHandlingComposesConsumerCustomizationRegardlessOfRegistrationOrder(bool consumerRegistersFirst)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var consumerCalled = false;
        void ConsumerCustomization(ProblemDetailsContext context) => consumerCalled = true;

        if (consumerRegistersFirst)
        {
            services.Configure<ProblemDetailsOptions>(options => options.CustomizeProblemDetails = ConsumerCustomization);
            services.AddProblemDetailsHandling();
        }
        else
        {
            services.AddProblemDetailsHandling();
            services.Configure<ProblemDetailsOptions>(options => options.CustomizeProblemDetails = ConsumerCustomization);
        }

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ProblemDetailsOptions>>().Value;
        var problemDetails = new ProblemDetails();
        var context = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext(),
            ProblemDetails = problemDetails
        };

        // Act
        options.CustomizeProblemDetails!.Invoke(context);

        // Assert
        Assert.True(consumerCalled);
        Assert.True(problemDetails.Extensions.ContainsKey("requestId"));
    }

    [Fact]
    public void AddProblemDetailsHandlingThrowsOnNullServices()
    {
        // Arrange

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ProblemDetailsExtensions.AddProblemDetailsHandling(null!));
    }

    [Fact]
    public void UseProblemDetailsHandlingRegistersMiddlewareAndReturnsTheApplicationBuilder()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddProblemDetailsHandling();
        using var app = builder.Build();

        // Act
        var result = app.UseProblemDetailsHandling();

        // Assert
        Assert.Same(app, result);
    }

    [Fact]
    public void UseProblemDetailsHandlingThrowsOnNullApp()
    {
        // Arrange

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ProblemDetailsExtensions.UseProblemDetailsHandling(null!));
    }

    [Fact]
    public void ComposedCustomizationSetsInstanceFromRequestMethodAndPathWhenNotAlreadySet()
    {
        // Arrange
        var customize = CreateComposedCustomization();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/widgets/1";
        var problemDetails = new ProblemDetails();
        var context = new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problemDetails };

        // Act
        customize.Invoke(context);

        // Assert
        Assert.Equal("GET /widgets/1", problemDetails.Instance);
    }

    [Fact]
    public void ComposedCustomizationPreservesAnAlreadySetInstance()
    {
        // Arrange
        var customize = CreateComposedCustomization();
        var problemDetails = new ProblemDetails { Instance = "custom-instance" };
        var context = new ProblemDetailsContext { HttpContext = new DefaultHttpContext(), ProblemDetails = problemDetails };

        // Act
        customize.Invoke(context);

        // Assert
        Assert.Equal("custom-instance", problemDetails.Instance);
    }

    [Fact]
    public void ComposedCustomizationDoesNotAddTraceIdExtensionWhenNoActivityIsCurrent()
    {
        // Arrange
        var previousActivity = Activity.Current;
        Activity.Current = null;
        try
        {
            var customize = CreateComposedCustomization();
            var problemDetails = new ProblemDetails();
            var context = new ProblemDetailsContext { HttpContext = new DefaultHttpContext(), ProblemDetails = problemDetails };

            // Act
            customize.Invoke(context);

            // Assert
            Assert.False(problemDetails.Extensions.ContainsKey("traceId"));
        }
        finally
        {
            Activity.Current = previousActivity;
        }
    }

    [Fact]
    public void ComposedCustomizationAddsTraceIdExtensionWhenAnActivityIsCurrent()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        using var activitySource = new ActivitySource(nameof(ComposedCustomizationAddsTraceIdExtensionWhenAnActivityIsCurrent));
        using var activity = activitySource.StartActivity("test-activity");
        Assert.NotNull(activity);
        var customize = CreateComposedCustomization();
        var problemDetails = new ProblemDetails();
        var context = new ProblemDetailsContext { HttpContext = new DefaultHttpContext(), ProblemDetails = problemDetails };

        // Act
        customize.Invoke(context);

        // Assert
        Assert.Equal(activity.Id, problemDetails.Extensions["traceId"]);
    }

    private static Action<ProblemDetailsContext> CreateComposedCustomization()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetailsHandling();
        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<ProblemDetailsOptions>>().Value.CustomizeProblemDetails!;
    }
}
