using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Vulthil.Extensions.Hosting;
using Vulthil.Extensions.Testing;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.Messaging;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.RabbitMq;
using Vulthil.Results;
using Vulthil.xUnit;
using Vulthil.xUnit.Fixtures;

namespace Vulthil.IntegrationTests;

/// <summary>
/// Pauses and resumes the message consumer host against a real RabbitMQ broker, the way a test harness does around
/// a reset: while it is paused, a published message waits in its queue; after the resume, new consumers deliver it.
/// </summary>
public sealed class RabbitMqConsumerPauseIntegrationTests(RabbitMqConsumerPauseIntegrationTests.BrokerHostFixture fixture)
    : BaseUnitTestCase, IClassFixture<RabbitMqConsumerPauseIntegrationTests.BrokerHostFixture>
{
    [Fact]
    public async Task APausedConsumerHostLeavesMessagesInTheQueueAndDeliversThemOnceResumed()
    {
        // Arrange
        var probe = new PauseProbe(Guid.NewGuid());
        var consumerHost = fixture.Services.GetServices<IHostedService>().OfType<IRestartableHostedService>().Single();
        await consumerHost.StopAsync(CancellationToken);

        // Act
        await PublishAsync(probe);
        var waitingInTheQueue = await fixture.WaitForReadyMessagesAsync(1, CancellationToken);
        var deliveredWhilePaused = fixture.Receipts.WasReceived(probe.Id);
        await consumerHost.StartAsync(CancellationToken);
        await fixture.Receipts.WaitForReceiptAsync(probe.Id, TimeSpan.FromSeconds(30), CancellationToken);

        // Assert
        waitingInTheQueue.ShouldBeTrue();
        deliveredWhilePaused.ShouldBeFalse();
        fixture.Receipts.ReceiptCount(probe.Id).ShouldBe(1);
    }

    private async Task PublishAsync(PauseProbe probe)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IPublisher>().PublishAsync(probe, CancellationToken);
    }

    /// <summary>
    /// Boots a minimal generic host with the real RabbitMQ transport against its own virtual host on the shared
    /// broker, with one queue whose consumer records every message it receives.
    /// </summary>
    public sealed class BrokerHostFixture(IntegrationTestContainerHost containerHost) : IAsyncLifetime
    {
        public const string QueueName = "consumer-pause";

        private const string BusHealthCheckName = "vulthil_messaging_rabbitmq_bus";

        private ITestContainer? _brokerScope;
        private IHost? _host;

        public IServiceProvider Services => _host?.Services
            ?? throw new InvalidOperationException("The fixture has not been initialized.");

        public ReceiptLog Receipts { get; } = new();

        public async ValueTask InitializeAsync()
        {
            var container = containerHost.Containers.OfType<RabbitMqTestContainer>().Single();
            await containerHost.EnsureStartedAsync(container);

            _brokerScope = ((ITestContainerScopeProvider)container).CreateScope($"consumerpause_{Guid.NewGuid().ToString("N")[..8]}");
            await _brokerScope.InitializeAsync();
            var connectionSource = (ITestContainerWithConnectionString)_brokerScope;

            var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
            builder.Configuration[$"ConnectionStrings:{connectionSource.ConnectionStringKey}"] = connectionSource.ConnectionString;
            builder.Services.AddSingleton(Receipts);
            builder.AddMessaging(messaging =>
            {
                messaging.ConfigureQueue(QueueName, queue =>
                {
                    queue.Subscribe<PauseProbe>();
                    queue.AddConsumer<RecordingConsumer>();
                });
                messaging.UseRabbitMq(connectionSource.ConnectionStringKey);
            });

            _host = builder.Build();
            await _host.StartAsync();
            await WaitForTheBusAsync(_host.Services.GetRequiredService<HealthCheckService>());
        }

        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);

            if (_host is not null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }

            await (_brokerScope?.DisposeAsync() ?? ValueTask.CompletedTask);
        }

        /// <summary>
        /// Waits until the queue holds <paramref name="count"/> messages that no consumer has taken. A message the broker
        /// has handed to a consumer is no longer counted as ready, so the count reaching the target shows that nothing
        /// is consuming the queue.
        /// </summary>
        public async Task<bool> WaitForReadyMessagesAsync(uint count, CancellationToken cancellationToken)
        {
            var connection = Services.GetRequiredService<IConnection>();
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            var reached = await Polling.WaitAsync(
                TimeSpan.FromSeconds(30),
                async token =>
                {
                    var queue = await channel.QueueDeclarePassiveAsync(QueueName, token);
                    return queue.MessageCount == count
                        ? Result.Success()
                        : Result.Failure(Error.Failure("RabbitMq.Queue.Count", $"The queue holds {queue.MessageCount} ready message(s)."));
                },
                TimeSpan.FromMilliseconds(100),
                cancellationToken);

            return reached.IsSuccess;
        }

        /// <summary>
        /// Waits until the bus reports ready. The host starts the bus in the background and publishing does not wait
        /// for it, so a publish sent before the bus has declared its exchanges and queues would be lost.
        /// </summary>
        private static async Task WaitForTheBusAsync(HealthCheckService healthChecks)
        {
            var started = await Polling.WaitAsync(
                TimeSpan.FromSeconds(60),
                async cancellationToken =>
                {
                    var report = await healthChecks.CheckHealthAsync(registration => registration.Name == BusHealthCheckName, cancellationToken);
                    return report.Status == HealthStatus.Healthy
                        ? Result.Success()
                        : Result.Failure(Error.Failure("RabbitMq.Bus.Starting", "The RabbitMQ bus has not finished starting."));
                },
                TimeSpan.FromMilliseconds(200),
                TestContext.Current.CancellationToken);

            started.IsSuccess.ShouldBeTrue("The RabbitMQ bus did not start.");
        }
    }

    /// <summary>
    /// Counts the deliveries of each message and completes a waiter when a message is first received.
    /// </summary>
    public sealed class ReceiptLog
    {
        private readonly ConcurrentDictionary<Guid, int> _receipts = new();
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _received = new();

        public void Record(Guid messageId)
        {
            _receipts.AddOrUpdate(messageId, 1, static (_, receipts) => receipts + 1);
            Received(messageId).TrySetResult();
        }

        public bool WasReceived(Guid messageId) => _receipts.ContainsKey(messageId);

        public int ReceiptCount(Guid messageId) => _receipts.GetValueOrDefault(messageId);

        public Task WaitForReceiptAsync(Guid messageId, TimeSpan timeout, CancellationToken cancellationToken) =>
            Received(messageId).Task.WaitAsync(timeout, cancellationToken);

        private TaskCompletionSource Received(Guid messageId) =>
            _received.GetOrAdd(messageId, static _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
    }

    public sealed class RecordingConsumer(ReceiptLog receipts) : IConsumer<PauseProbe>
    {
        public Task ConsumeAsync(IMessageContext<PauseProbe> messageContext, CancellationToken cancellationToken = default)
        {
            receipts.Record(messageContext.Message.Id);
            return Task.CompletedTask;
        }
    }

    public sealed record PauseProbe(Guid Id);
}
