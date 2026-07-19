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
}
