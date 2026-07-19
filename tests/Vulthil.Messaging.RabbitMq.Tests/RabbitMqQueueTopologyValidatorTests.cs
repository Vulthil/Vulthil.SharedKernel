using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class RabbitMqQueueTopologyValidatorTests : BaseUnitTestCase
{
    private readonly Lazy<RabbitMqQueueTopologyValidator> _lazyTarget;
    private RabbitMqQueueTopologyValidator Target => _lazyTarget.Value;

    public RabbitMqQueueTopologyValidatorTests() => _lazyTarget = new(CreateInstance<RabbitMqQueueTopologyValidator>);

    private static IMessageConfigurationProvider BuildProvider(MessagingExchangeType exchangeType)
        => TestProviders.Build(cfg => cfg.ConfigureQueue("orders", queue =>
        {
            queue.AddConsumer<OrderNoteConsumer>();
            queue.ConfigureQueue(definition => definition.ExchangeType = exchangeType);
        }));

    [Theory]
    [InlineData(MessagingExchangeType.Direct)]
    [InlineData(MessagingExchangeType.Topic)]
    [InlineData(MessagingExchangeType.Headers)]
    public void NonFanoutQueueExchangeTypeFailsStartupValidation(MessagingExchangeType exchangeType)
    {
        // Arrange
        Use(BuildProvider(exchangeType));

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
        Use(BuildProvider(MessagingExchangeType.Fanout));

        // Act
        var result = Target.Validate(null, new RabbitMqTransportOptions());

        // Assert
        result.Succeeded.ShouldBeTrue();
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
