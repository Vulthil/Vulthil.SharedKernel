using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace Vulthil.Messaging.IntegrationTest.Tests;

public sealed class AppHostFixture : IAsyncLifetime
{
    private DistributedApplication? _app;
    private HttpClient? _producerClient;
    private HttpClient? _consumerClient;

    public DistributedApplication App => _app ?? throw new InvalidOperationException("AppHost has not been started.");
    public HttpClient ProducerClient => _producerClient ?? throw new InvalidOperationException("AppHost has not been started.");
    public HttpClient ConsumerClient => _consumerClient ?? throw new InvalidOperationException("AppHost has not been started.");

    public ValueTask<string?> GetRabbitMqConnectionStringAsync(CancellationToken cancellationToken = default)
        => App.GetConnectionStringAsync("rabbitmq", cancellationToken);

    public async ValueTask InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Vulthil_Messaging_IntegrationTest_AppHost>();

        _app = await builder.BuildAsync();
        await _app.StartAsync();

        using var startupCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("producer", startupCts.Token);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("consumer", startupCts.Token);

        _producerClient = _app.CreateHttpClient("producer", "http");
        _consumerClient = _app.CreateHttpClient("consumer", "http");
    }

    public async ValueTask DisposeAsync()
    {
        _producerClient?.Dispose();
        _consumerClient?.Dispose();

        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}

[CollectionDefinition(nameof(AppHostCollection))]
public sealed class AppHostCollection : ICollectionFixture<AppHostFixture>;
