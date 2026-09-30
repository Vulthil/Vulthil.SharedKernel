using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
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
/// Runs the documented delayed-retry policy (exponential intervals with jitter) against a real RabbitMQ broker. The
/// broker accepts only a whole number of milliseconds as a per-message TTL and closes the channel on anything else,
/// so this pins the path end to end: the first attempt fails, the worker republishes to the retry queue with the
/// jittered delay as the TTL, and the message returns through the dead-letter route and succeeds.
/// </summary>
public sealed class RabbitMqDelayedRetryIntegrationTests(RabbitMqDelayedRetryIntegrationTests.BrokerHostFixture fixture)
    : BaseUnitTestCase, IClassFixture<RabbitMqDelayedRetryIntegrationTests.BrokerHostFixture>
{
    [Fact]
    public async Task AFailedDeliveryComesBackThroughTheRetryQueueUnderTheDocumentedJitteredPolicy()
    {
        // Arrange
        var probe = new RetryProbe(Guid.NewGuid());
        await using var scope = fixture.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        // Act
        await publisher.PublishAsync(probe, CancellationToken);
        var attempts = await fixture.Attempts.WaitForSuccessAsync(probe.Id, TimeSpan.FromSeconds(30), CancellationToken);

        // Assert
        attempts.ShouldBe(2);
    }

    /// <summary>
    /// Boots a minimal generic host with the real RabbitMQ transport against its own virtual host on the shared
    /// broker: one queue whose consumer fails the first attempt at each message, retried through the delayed retry
    /// queue with the policy the messaging docs show.
    /// </summary>
    public sealed class BrokerHostFixture(IntegrationTestContainerHost containerHost) : IAsyncLifetime
    {
        private const string BusHealthCheckName = "vulthil_messaging_rabbitmq_bus";

        private ITestContainer? _brokerScope;
        private IHost? _host;

        public IServiceProvider Services => _host?.Services
            ?? throw new InvalidOperationException("The fixture has not been initialized.");

        public AttemptLog Attempts { get; } = new();

        public async ValueTask InitializeAsync()
        {
            var container = containerHost.Containers.OfType<RabbitMqTestContainer>().Single();
            await containerHost.EnsureStartedAsync(container);

            _brokerScope = ((ITestContainerScopeProvider)container).CreateScope($"delayedretry_{Guid.NewGuid().ToString("N")[..8]}");
            await _brokerScope.InitializeAsync();
            var connectionSource = (ITestContainerWithConnectionString)_brokerScope;

            var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
            builder.Configuration[$"ConnectionStrings:{connectionSource.ConnectionStringKey}"] = connectionSource.ConnectionString;
            builder.Services.AddSingleton(Attempts);
            builder.AddMessaging(messaging =>
            {
                messaging.ConfigureQueue("delayed-retry", queue =>
                {
                    queue.Subscribe<RetryProbe>();
                    queue.AddConsumer<FailFirstAttemptConsumer>();
                    queue.UseRetry(retry =>
                    {
                        retry.Exponential(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
                        retry.UseJitter(0.2);
                    });
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
    /// Counts the attempts at each message and completes a waiter when that message's consumer succeeds.
    /// </summary>
    public sealed class AttemptLog
    {
        private readonly ConcurrentDictionary<Guid, int> _attempts = new();
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<int>> _successes = new();

        public int RecordAttempt(Guid messageId) => _attempts.AddOrUpdate(messageId, 1, static (_, attempts) => attempts + 1);

        public void RecordSuccess(Guid messageId, int attempts) => Success(messageId).TrySetResult(attempts);

        public Task<int> WaitForSuccessAsync(Guid messageId, TimeSpan timeout, CancellationToken cancellationToken) =>
            Success(messageId).Task.WaitAsync(timeout, cancellationToken);

        private TaskCompletionSource<int> Success(Guid messageId) =>
            _successes.GetOrAdd(messageId, static _ => new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously));
    }

    public sealed class FailFirstAttemptConsumer(AttemptLog attempts) : IConsumer<RetryProbe>
    {
        public Task ConsumeAsync(IMessageContext<RetryProbe> messageContext, CancellationToken cancellationToken = default)
        {
            var attempt = attempts.RecordAttempt(messageContext.Message.Id);
            if (attempt == 1)
            {
                throw new InvalidOperationException("The first attempt at each message fails.");
            }

            attempts.RecordSuccess(messageContext.Message.Id, attempt);
            return Task.CompletedTask;
        }
    }

    public sealed record RetryProbe(Guid Id);
}
