using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests;

public sealed class PartitionRegistrationTests : BaseUnitTestCase<HostApplicationBuilder>
{
    private readonly Mock<IMessageContext<OrderUpdated>> _context;
    private readonly Partitioner _partitioner = new(16);
    private readonly Partitioner _otherPartitioner = new(16);

    public PartitionRegistrationTests()
    {
        _context = GetMock<IMessageContext<OrderUpdated>>();
        _context.SetupGet(context => context.Message).Returns(new OrderUpdated("order-1", "customer-1"));
        _context.SetupGet(context => context.CorrelationId).Returns("correlation-1");
    }

    public enum Registration
    {
        CountByOrderId,
        CountByCustomerId,
        OtherCountByOrderId,
        CountByCorrelationId,
        CountByOwnCorrelationIdKey,
        PartitionerByOrderId,
        PartitionerByCustomerId,
        OtherPartitionerByOrderId,
        PartitionerByCorrelationId,
    }

    protected override HostApplicationBuilder CreateInstance() => Host.CreateApplicationBuilder();

    [Theory]
    [InlineData(Registration.CountByOrderId, "order-1")]
    [InlineData(Registration.CountByCorrelationId, "correlation-1")]
    [InlineData(Registration.PartitionerByOrderId, "order-1")]
    [InlineData(Registration.PartitionerByCorrelationId, "correlation-1")]
    public void RepeatingARegistrationKeepsIt(Registration registration, string expectedKey)
    {
        // Arrange
        Action<IMessagingConfigurator> configure = messaging =>
        {
            Register(messaging, registration);
            Register(messaging, registration);
        };

        // Act
        Target.AddMessaging(configure);

        // Assert
        var partition = GetPartition<OrderUpdated>();
        partition.Partitioner.PartitionCount.ShouldBe(16);
        var keySelector = partition.KeySelector.ShouldBeAssignableTo<Func<IMessageContext<OrderUpdated>, string?>>();
        keySelector(_context.Object).ShouldBe(expectedKey);
    }

    [Theory]
    [InlineData(Registration.CountByOrderId, Registration.CountByCustomerId)]
    [InlineData(Registration.CountByOrderId, Registration.OtherCountByOrderId)]
    [InlineData(Registration.CountByCorrelationId, Registration.CountByOwnCorrelationIdKey)]
    [InlineData(Registration.CountByOrderId, Registration.PartitionerByOrderId)]
    [InlineData(Registration.PartitionerByOrderId, Registration.CountByOrderId)]
    [InlineData(Registration.PartitionerByOrderId, Registration.PartitionerByCustomerId)]
    [InlineData(Registration.PartitionerByOrderId, Registration.OtherPartitionerByOrderId)]
    [InlineData(Registration.PartitionerByCorrelationId, Registration.CountByCorrelationId)]
    public void ADifferentSecondRegistrationForTheSameMessageTypeThrows(Registration first, Registration second)
    {
        // Arrange
        Action<IMessagingConfigurator> configure = messaging =>
        {
            Register(messaging, first);
            Register(messaging, second);
        };

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => Target.AddMessaging(configure));
        exception.Message.ShouldContain(typeof(OrderUpdated).FullName!);
    }

    [Fact]
    public void OnePartitionerCanServeSeveralMessageTypes()
    {
        // Arrange
        Action<IMessagingConfigurator> configure = messaging =>
        {
            messaging.UsePartitioner<OrderUpdated>(_partitioner);
            messaging.UsePartitioner<OrderShipped>(_partitioner);
        };

        // Act
        Target.AddMessaging(configure);

        // Assert
        GetPartition<OrderUpdated>().Partitioner.ShouldBeSameAs(_partitioner);
        GetPartition<OrderShipped>().Partitioner.ShouldBeSameAs(_partitioner);
    }

    [Fact]
    public void APartitionCountGivesEachMessageTypeAPartitionerOfItsOwn()
    {
        // Arrange
        Action<IMessagingConfigurator> configure = messaging =>
        {
            messaging.UsePartitioner<OrderUpdated>(16);
            messaging.UsePartitioner<OrderShipped>(16);
        };

        // Act
        Target.AddMessaging(configure);

        // Assert
        GetPartition<OrderUpdated>().Partitioner.ShouldNotBeSameAs(GetPartition<OrderShipped>().Partitioner);
    }

    private void Register(IMessagingConfigurator messaging, Registration registration)
    {
        switch (registration)
        {
            case Registration.CountByOrderId:
                messaging.UsePartitioner<OrderUpdated>(16, OrderIdKey);
                break;
            case Registration.CountByCustomerId:
                messaging.UsePartitioner<OrderUpdated>(16, CustomerIdKey);
                break;
            case Registration.OtherCountByOrderId:
                messaging.UsePartitioner<OrderUpdated>(8, OrderIdKey);
                break;
            case Registration.CountByCorrelationId:
                messaging.UsePartitioner<OrderUpdated>(16);
                break;
            case Registration.CountByOwnCorrelationIdKey:
                messaging.UsePartitioner<OrderUpdated>(16, CorrelationIdKey);
                break;
            case Registration.PartitionerByOrderId:
                messaging.UsePartitioner<OrderUpdated>(_partitioner, OrderIdKey);
                break;
            case Registration.PartitionerByCustomerId:
                messaging.UsePartitioner<OrderUpdated>(_partitioner, CustomerIdKey);
                break;
            case Registration.OtherPartitionerByOrderId:
                messaging.UsePartitioner<OrderUpdated>(_otherPartitioner, OrderIdKey);
                break;
            case Registration.PartitionerByCorrelationId:
                messaging.UsePartitioner<OrderUpdated>(_partitioner);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(registration), registration, null);
        }
    }

    private PartitionSpec GetPartition<TMessage>()
    {
        using var provider = Target.Services.BuildServiceProvider();
        var partition = provider.GetRequiredService<IMessageConfigurationProvider>().GetPartition(typeof(TMessage));
        partition.ShouldNotBeNull();
        return partition;
    }

    private static string? OrderIdKey(IMessageContext<OrderUpdated> context) => context.Message.OrderId;

    private static string? CustomerIdKey(IMessageContext<OrderUpdated> context) => context.Message.CustomerId;

    private static string? CorrelationIdKey(IMessageContext<OrderUpdated> context) => context.CorrelationId;

    public sealed record OrderUpdated(string OrderId, string CustomerId);

    public sealed record OrderShipped(string OrderId);
}
