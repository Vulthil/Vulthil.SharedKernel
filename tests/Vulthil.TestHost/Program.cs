// Minimal composition root hosting the Vulthil infrastructure (PostgreSQL with outbox and inbox, messaging with a
// probe consumer pipeline) so the integration tests exercise library behavior against a real host.
using Vulthil.Messaging;
using Vulthil.Messaging.Inbox;
using Vulthil.Messaging.Inbox.Relational;
using Vulthil.Messaging.Outbox;
using Vulthil.Messaging.RabbitMq;
using Vulthil.SharedKernel.Application;
using Vulthil.SharedKernel.Infrastructure;
using Vulthil.SharedKernel.Infrastructure.Npgsql;
using Vulthil.TestHost;
using Vulthil.TestHost.Data;
using Vulthil.TestHost.Probes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication(application =>
    application.RegisterHandlerAssemblies(typeof(TestHostDbContext).Assembly));

builder.AddDbContext<TestHostDbContext>(database => database
    .UseNpgsql(TestHostConnectionStrings.Postgres)
    .EnableOutboxProcessing());

builder.Services.AddRelationalInbox<TestHostDbContext>();

builder.AddMessaging(messaging =>
{
    messaging.ConfigureQueue("test-host", queue =>
    {
        queue.AddRequestConsumer<GetProbeSideEffectsConsumer>();

        queue.Subscribe<ProbeCreatedIntegrationEvent>();
        queue.AddConsumer<ProbeCreatedIntegrationEventConsumer>();
    });

    messaging.AddIdempotentInbox<ProbeCreatedIntegrationEvent>(context => context.Message.Id.ToString());
    messaging.AddTransactionalOutbox();

    messaging.UseRabbitMq(TestHostConnectionStrings.RabbitMq);
});

var app = builder.Build();

await app.RunAsync();

/// <summary>
/// Public entry-point marker so test factories can close <c>WebApplicationFactory&lt;TEntryPoint&gt;</c> over this host.
/// </summary>
public partial class Program
{
    protected Program()
    {
    }
}
