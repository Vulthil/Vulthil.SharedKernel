using System.Text;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;
public sealed class RabbitMqConsumerTests : BaseUnitTestCase
{
    private readonly MessageType _messageType;
    private readonly Mock<IChannel> _channelMock;
    private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock;
    private readonly Mock<IServiceScope> _serviceScopeMock;
    private readonly QueueDefinition _queueDef;
    private readonly TypeCache _typeCache;
    private readonly Lazy<RabbitMqConsumer> _lazyTarget;
    private RabbitMqConsumer Target => _lazyTarget.Value;

    private sealed record SomeType(string Name);
    private sealed class TestConsumer : IConsumer<SomeType>
    {
        public Task ConsumeAsync(SomeType message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public RabbitMqConsumerTests()
    {
        _messageType = new MessageType(typeof(SomeType));
        _channelMock = GetMock<IChannel>();
        _serviceScopeFactoryMock = GetMock<IServiceScopeFactory>();
        _serviceScopeMock = GetMock<IServiceScope>();
        _queueDef = new QueueDefinition("test-queue", new Dictionary<MessageType, List<ConsumerType>>
        {
            [_messageType] = [new ConsumerType(typeof(TestConsumer))]
        }, new Dictionary<ConsumerType, List<MessageType>>
        {

            [new ConsumerType(typeof(TestConsumer))] = [_messageType]
        });

        _typeCache = new TypeCache();
        _typeCache.AddTypeMap(_messageType);
        Use(_queueDef);
        Use(_typeCache);
        Use(new TestConsumer());

        _serviceScopeMock.SetupGet(x => x.ServiceProvider).Returns(AutoMocker);
        _serviceScopeFactoryMock.Setup(x => x.CreateScope()).Returns(_serviceScopeMock.Object);

        _lazyTarget = new Lazy<RabbitMqConsumer>(CreateInstance<RabbitMqConsumer>);

    }

    [Fact]
    public async Task ShouldAckMessageWhenReceived()
    {
        var body = Encoding.UTF8.GetBytes(@"{""Name"": ""Some Name""}");
        var propsMock = GetMock<IReadOnlyBasicProperties>();
        propsMock.SetupGet(x => x.Type).Returns(_messageType.Name);


        // Act
        await Target.HandleBasicDeliverAsync("consumerTag",
            1UL,
            false,
            "exchange",
            "routingKey",
            propsMock.Object,
            body,
            CancellationToken);


        // Assert
        _channelMock.Verify(x => x.BasicAckAsync(1UL, false, CancellationToken), Times.Once);
    }
}
