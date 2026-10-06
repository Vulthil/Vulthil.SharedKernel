using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Vulthil.Extensions.Testing;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.Messaging;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.RabbitMq;
using Vulthil.Messaging.Transport;
using Vulthil.Results;
using Vulthil.xUnit;
using Vulthil.xUnit.Fixtures;

namespace Vulthil.IntegrationTests;

/// <summary>
/// Sends deliveries with headers that another client wrote as strings straight to a real RabbitMQ broker. The client
/// writes a string header as an AMQP long string and the consumer side surfaces it as UTF-8 bytes, so this pins the
/// consume path end to end: a retry count sent as text counts as the round it names, and a delivery whose address
/// header the worker cannot parse goes to the dead-letter queue instead of staying unacknowledged.
/// </summary>
public sealed class RabbitMqForeignHeaderIntegrationTests(RabbitMqForeignHeaderIntegrationTests.BrokerHostFixture fixture)
    : BaseUnitTestCase, IClassFixture<RabbitMqForeignHeaderIntegrationTests.BrokerHostFixture>
{
    [Fact]
    public async Task ARetryCountSentAsTextIsConsumedAtTheRoundItNames()
    {
        // Arrange
        var probe = new HeaderProbe(Guid.NewGuid());

        // Act
        await fixture.SendEnvelopeAsync(probe, new Dictionary<string, object?> { ["x-retry-count"] = "2" }, CancellationToken);
        var round = await fixture.Rounds.WaitForRoundAsync(probe.Id, TimeSpan.FromSeconds(30), CancellationToken);

        // Assert
        round.ShouldBe(2);
    }

    [Fact]
    public async Task ADeliveryWithAnAddressHeaderTheWorkerCannotParseIsDeadLettered()
    {
        // Arrange
        var probe = new HeaderProbe(Guid.NewGuid());

        // Act
        await fixture.SendBareJsonAsync(probe, new Dictionary<string, object?> { [MessageHeaders.FaultAddress] = "//[" }, CancellationToken);
        var deadLettered = await fixture.WaitForDeadLetterAsync(probe.Id, CancellationToken);

        // Assert
        deadLettered.ShouldBeTrue();
        fixture.Rounds.WasConsumed(probe.Id).ShouldBeFalse();
    }

    /// <summary>
    /// Boots a minimal generic host with the real RabbitMQ transport against its own virtual host on the shared
    /// broker, with one dead-lettering queue whose consumer records the round each message is consumed at.
    /// </summary>
    public sealed class BrokerHostFixture(IntegrationTestContainerHost containerHost) : IAsyncLifetime
    {
        public const string QueueName = "foreign-headers";

        private const string DeadLetterQueueName = $"{QueueName}.Error";
        private const string BusHealthCheckName = "vulthil_messaging_rabbitmq_bus";

        private ITestContainer? _brokerScope;
        private IHost? _host;

        public IServiceProvider Services => _host?.Services
            ?? throw new InvalidOperationException("The fixture has not been initialized.");

        public RoundLog Rounds { get; } = new();

        public async ValueTask InitializeAsync()
        {
            var container = containerHost.Containers.OfType<RabbitMqTestContainer>().Single();
            await containerHost.EnsureStartedAsync(container);

            _brokerScope = ((ITestContainerScopeProvider)container).CreateScope($"foreignheaders_{Guid.NewGuid().ToString("N")[..8]}");
            await _brokerScope.InitializeAsync();
            var connectionSource = (ITestContainerWithConnectionString)_brokerScope;

            var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
            builder.Configuration[$"ConnectionStrings:{connectionSource.ConnectionStringKey}"] = connectionSource.ConnectionString;
            builder.Services.AddSingleton(Rounds);
            builder.AddMessaging(messaging =>
            {
                messaging.ConfigureQueue(QueueName, queue =>
                {
                    queue.Subscribe<HeaderProbe>();
                    queue.AddConsumer<RoundRecordingConsumer>();
                    queue.UseDeadLetterQueue();
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
        /// Sends <paramref name="probe"/> to the queue in a Vulthil envelope, as a message re-published from the
        /// management UI arrives: the envelope as the body and the given headers as strings.
        /// </summary>
        public Task SendEnvelopeAsync(HeaderProbe probe, Dictionary<string, object?> headers, CancellationToken cancellationToken)
        {
            var provider = Services.GetRequiredService<IMessageConfigurationProvider>();
            var urn = provider.GetUrn(typeof(HeaderProbe));
            var envelope = MessageEnvelopeFactory.Create(
                probe, new PublishContext(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), urn, provider.JsonSerializerOptions);
            return SendAsync(urn.AbsoluteUri, headers, JsonSerializer.SerializeToUtf8Bytes(envelope, provider.JsonSerializerOptions), cancellationToken);
        }

        /// <summary>
        /// Sends <paramref name="probe"/> to the queue as bare JSON, as a client without the Vulthil envelope sends it.
        /// </summary>
        public Task SendBareJsonAsync(HeaderProbe probe, Dictionary<string, object?> headers, CancellationToken cancellationToken)
        {
            var provider = Services.GetRequiredService<IMessageConfigurationProvider>();
            return SendAsync(typeof(HeaderProbe).FullName!, headers, JsonSerializer.SerializeToUtf8Bytes(probe, provider.JsonSerializerOptions), cancellationToken);
        }

        /// <summary>
        /// Waits until the dead-letter queue holds the message of <paramref name="probeId"/>.
        /// </summary>
        public async Task<bool> WaitForDeadLetterAsync(Guid probeId, CancellationToken cancellationToken)
        {
            var connection = Services.GetRequiredService<IConnection>();
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            var found = await Polling.WaitAsync(
                TimeSpan.FromSeconds(30),
                async token =>
                {
                    var message = await channel.BasicGetAsync(DeadLetterQueueName, autoAck: true, token);
                    return message is not null && Encoding.UTF8.GetString(message.Body.Span).Contains(probeId.ToString(), StringComparison.Ordinal)
                        ? Result.Success()
                        : Result.Failure(Error.Failure("RabbitMq.DeadLetter.Missing", "The message has not been dead-lettered."));
                },
                TimeSpan.FromMilliseconds(100),
                cancellationToken);

            return found.IsSuccess;
        }

        private async Task SendAsync(string type, Dictionary<string, object?> headers, byte[] body, CancellationToken cancellationToken)
        {
            var connection = Services.GetRequiredService<IConnection>();
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            var properties = new BasicProperties { Type = type, ContentType = "application/json", Headers = headers };
            await channel.BasicPublishAsync(string.Empty, QueueName, mandatory: true, properties, body, cancellationToken);
        }

        /// <summary>
        /// Waits until the bus reports ready. The host starts the bus in the background, so a message sent before the
        /// bus has declared its queues would be lost.
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
    /// Records the round each message is consumed at and completes a waiter when it is.
    /// </summary>
    public sealed class RoundLog
    {
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<int>> _rounds = new();

        public void Record(Guid messageId, int round) => Round(messageId).TrySetResult(round);

        public bool WasConsumed(Guid messageId) => _rounds.TryGetValue(messageId, out var round) && round.Task.IsCompleted;

        public Task<int> WaitForRoundAsync(Guid messageId, TimeSpan timeout, CancellationToken cancellationToken) =>
            Round(messageId).Task.WaitAsync(timeout, cancellationToken);

        private TaskCompletionSource<int> Round(Guid messageId) =>
            _rounds.GetOrAdd(messageId, static _ => new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously));
    }

    public sealed class RoundRecordingConsumer(RoundLog rounds) : IConsumer<HeaderProbe>
    {
        public Task ConsumeAsync(IMessageContext<HeaderProbe> messageContext, CancellationToken cancellationToken = default)
        {
            rounds.Record(messageContext.Message.Id, messageContext.RetryCount);
            return Task.CompletedTask;
        }
    }

    public sealed record HeaderProbe(Guid Id);
}
