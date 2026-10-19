using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class RabbitMqQueueTopologyValidatorTests : BaseUnitTestCase
{
    private readonly Lazy<RabbitMqQueueTopologyValidator> _lazyTarget;
    private RabbitMqQueueTopologyValidator Target => _lazyTarget.Value;

    public RabbitMqQueueTopologyValidatorTests() => _lazyTarget = new(CreateInstance<RabbitMqQueueTopologyValidator>);

    private static IMessageConfigurationProvider BuildProvider(Action<QueueDefinition> configureQueue)
        => TestProviders.Build(cfg => cfg.ConfigureQueue("orders", queue =>
        {
            queue.AddConsumer<OrderNoteConsumer>();
            queue.ConfigureQueue(configureQueue);
        }));

    [Theory]
    [InlineData(MessagingExchangeType.Direct)]
    [InlineData(MessagingExchangeType.Topic)]
    [InlineData(MessagingExchangeType.Headers)]
    public void NonFanoutQueueExchangeTypeFailsStartupValidation(MessagingExchangeType exchangeType)
    {
        // Arrange
        Use(BuildProvider(definition => definition.ExchangeType = exchangeType));

        // Act
        var result = Target.Validate(null, new RabbitMqTransportOptions());

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("orders");
        result.FailureMessage.ShouldContain(exchangeType.ToString());
        result.FailureMessage.ShouldContain("must be Fanout");
    }

    [Fact]
    public void FanoutQueueExchangeTypePassesStartupValidation()
    {
        // Arrange
        Use(BuildProvider(definition => definition.ExchangeType = MessagingExchangeType.Fanout));

        // Act
        var result = Target.Validate(null, new RabbitMqTransportOptions());

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void ExclusiveQuorumQueueFailsStartupValidation()
    {
        // Arrange
        Use(BuildProvider(definition => definition.Exclusive = true));

        // Act
        var result = Target.Validate(null, new RabbitMqTransportOptions());

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("orders");
        result.FailureMessage.ShouldContain("sets Exclusive");
        result.FailureMessage.ShouldContain("quorum queue");
    }

    [Fact]
    public void ExclusiveClassicQueuePassesStartupValidation()
    {
        // Arrange
        Use(BuildProvider(definition =>
        {
            definition.IsQuorum = false;
            definition.Exclusive = true;
        }));

        // Act
        var result = Target.Validate(null, new RabbitMqTransportOptions());

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void QueueWithANonFanoutExchangeThatIsAlsoAnExclusiveQuorumQueueReportsBothFailures()
    {
        // Arrange
        Use(BuildProvider(definition =>
        {
            definition.ExchangeType = MessagingExchangeType.Direct;
            definition.Exclusive = true;
        }));

        // Act
        var result = Target.Validate(null, new RabbitMqTransportOptions());

        // Assert
        result.Failures.ShouldNotBeNull().Count().ShouldBe(2);
    }

    [Fact]
    public void UseRabbitMqRegistersTheTopologyValidator()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        // Act
        builder.AddMessaging(cfg => cfg.UseRabbitMq());

        // Assert
        builder.Services.ShouldContain(descriptor =>
            descriptor.ServiceType == typeof(IValidateOptions<RabbitMqTransportOptions>)
            && descriptor.ImplementationType == typeof(RabbitMqQueueTopologyValidator));
    }

    public sealed record OrderNote(string Text);

    public sealed class OrderNoteConsumer : IConsumer<OrderNote>
    {
        public Task ConsumeAsync(IMessageContext<OrderNote> messageContext, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
