using Vulthil.SharedKernel.Application.Behaviors;
using Vulthil.SharedKernel.Application.Pipeline;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Application.Tests;

public sealed class ApplicationOptionsTests : BaseUnitTestCase<ApplicationOptions>
{
    [Fact]
    public void AddRequestLoggingPipelineBehaviorRegistersTheRequestLoggingBehavior()
    {
        // Act
        var returned = Target.AddRequestLoggingPipelineBehavior();

        // Assert
        returned.ShouldBeSameAs(Target);
        var descriptor = Target.HandlerOptions.PipelineHandlers.ShouldHaveSingleItem();
        descriptor.ServiceType.ShouldBe(typeof(IPipelineHandler<,>));
        descriptor.ImplementationType.ShouldBe(typeof(LoggingBehaviors.RequestLoggingPipelineBehavior<,>));
    }

    [Fact]
    public void AddDomainEventLoggingPipelineBehaviorRegistersTheDomainEventLoggingBehavior()
    {
        // Act
        var returned = Target.AddDomainEventLoggingPipelineBehavior();

        // Assert
        returned.ShouldBeSameAs(Target);
        var descriptor = Target.HandlerOptions.PipelineHandlers.ShouldHaveSingleItem();
        descriptor.ServiceType.ShouldBe(typeof(IDomainEventPipelineHandler<>));
        descriptor.ImplementationType.ShouldBe(typeof(LoggingBehaviors.DomainEventLoggingPipelineBehavior<>));
    }
}
