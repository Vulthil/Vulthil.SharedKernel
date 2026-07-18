using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.SharedKernel.Infrastructure;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.SharedKernel.Infrastructure.MySql;
using Vulthil.SharedKernel.Infrastructure.Npgsql;
using Vulthil.xUnit.Fixtures;

namespace Vulthil.IntegrationTests.Fixtures;

/// <summary>
/// Boots a minimal generic host through the real provider registration path (<c>AddDbContext</c> + provider
/// <c>Use*</c> + <c>EnableOutboxProcessing</c>) against a per-class database on a shared container. The host is
/// built but never started, so no background service runs: tests drive the outbox store directly for deterministic
/// relay batches.
/// </summary>
/// <typeparam name="TDbContext">The provider-mapped context under test.</typeparam>
public abstract class ProviderOutboxHostFixture<TDbContext>(IntegrationTestContainerHost containerHost) : IAsyncLifetime
    where TDbContext : BaseDbContext
{
    private ITestContainer? _databaseScope;
    private IHost? _host;

    public IServiceProvider Services => _host?.Services
        ?? throw new InvalidOperationException("The fixture has not been initialized.");

    protected abstract ITestContainer SelectContainer(IntegrationTestContainerHost host);

    protected abstract void RegisterDatabase(IHostApplicationBuilder builder, string connectionStringKey);

    public async ValueTask InitializeAsync()
    {
        var container = SelectContainer(containerHost);
        await containerHost.EnsureStartedAsync(container);

        _databaseScope = ((ITestContainerScopeProvider)container).CreateScope(CreateScopeId());
        await _databaseScope.InitializeAsync();
        var connectionSource = (ITestContainerWithConnectionString)_databaseScope;

        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration[$"ConnectionStrings:{connectionSource.ConnectionStringKey}"] = connectionSource.ConnectionString;
        RegisterDatabase(builder, connectionSource.ConnectionStringKey);

        _host = builder.Build();
        await _host.Services.EnsureCreatedAsync<TDbContext>();
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        _host?.Dispose();
        await (_databaseScope?.DisposeAsync() ?? ValueTask.CompletedTask);
    }

    /// <summary>
    /// Deletes every outbox row (and any provider-specific probe state) so the next test starts from a clean slate.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async Task ResetOutboxStateAsync(CancellationToken cancellationToken)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await context.OutboxMessages.ExecuteDeleteAsync(cancellationToken);
        await ResetAdditionalStateAsync(context, cancellationToken);
    }

    protected virtual Task ResetAdditionalStateAsync(TDbContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    private string CreateScopeId()
    {
        var name = new string([.. GetType().Name.Where(char.IsAsciiLetterOrDigit).Select(char.ToLowerInvariant)]);
        if (name.Length > 24)
        {
            name = name[..24];
        }

        return $"{name}_{Guid.NewGuid().ToString("N")[..8]}";
    }
}

/// <summary>
/// Host fixture for the MySQL outbox tests: registers <see cref="MySqlOutboxDbContext"/> through <c>UseMySql</c>.
/// </summary>
public sealed class MySqlOutboxHostFixture(IntegrationTestContainerHost containerHost) : ProviderOutboxHostFixture<MySqlOutboxDbContext>(containerHost)
{
    protected override ITestContainer SelectContainer(IntegrationTestContainerHost host) =>
        host.Containers.OfType<MySqlTestContainer>().Single();

    protected override void RegisterDatabase(IHostApplicationBuilder builder, string connectionStringKey) =>
        builder.AddDbContext<MySqlOutboxDbContext>(database => database
            .UseMySql(connectionStringKey)
            .EnableOutboxProcessing());

    protected override Task ResetAdditionalStateAsync(MySqlOutboxDbContext context, CancellationToken cancellationToken) =>
        context.Probes.ExecuteDeleteAsync(cancellationToken);
}

/// <summary>
/// Host fixture for the renamed-model MySQL relay test: registers <see cref="RenamedMySqlOutboxDbContext"/>.
/// </summary>
public sealed class RenamedMySqlOutboxHostFixture(IntegrationTestContainerHost containerHost) : ProviderOutboxHostFixture<RenamedMySqlOutboxDbContext>(containerHost)
{
    protected override ITestContainer SelectContainer(IntegrationTestContainerHost host) =>
        host.Containers.OfType<MySqlTestContainer>().Single();

    protected override void RegisterDatabase(IHostApplicationBuilder builder, string connectionStringKey) =>
        builder.AddDbContext<RenamedMySqlOutboxDbContext>(database => database
            .UseMySql(connectionStringKey)
            .EnableOutboxProcessing());
}

/// <summary>
/// Host fixture for the relational outbox store tests on the reference dialect: registers
/// <see cref="NpgsqlOutboxDbContext"/> through <c>UseNpgsql</c>.
/// </summary>
public sealed class NpgsqlOutboxHostFixture(IntegrationTestContainerHost containerHost) : ProviderOutboxHostFixture<NpgsqlOutboxDbContext>(containerHost)
{
    protected override ITestContainer SelectContainer(IntegrationTestContainerHost host) =>
        host.Containers.OfType<PostgreSqlTestContainer>().Single();

    protected override void RegisterDatabase(IHostApplicationBuilder builder, string connectionStringKey) =>
        builder.AddDbContext<NpgsqlOutboxDbContext>(database => database
            .UseNpgsql(connectionStringKey)
            .EnableOutboxProcessing());
}

/// <summary>
/// Host fixture for the renamed-model PostgreSQL relay test: registers <see cref="RenamedNpgsqlOutboxDbContext"/>.
/// </summary>
public sealed class RenamedNpgsqlOutboxHostFixture(IntegrationTestContainerHost containerHost) : ProviderOutboxHostFixture<RenamedNpgsqlOutboxDbContext>(containerHost)
{
    protected override ITestContainer SelectContainer(IntegrationTestContainerHost host) =>
        host.Containers.OfType<PostgreSqlTestContainer>().Single();

    protected override void RegisterDatabase(IHostApplicationBuilder builder, string connectionStringKey) =>
        builder.AddDbContext<RenamedNpgsqlOutboxDbContext>(database => database
            .UseNpgsql(connectionStringKey)
            .EnableOutboxProcessing());
}
