using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Vulthil.SharedKernel.Infrastructure;
using Vulthil.SharedKernel.Infrastructure.Cosmos;
using Vulthil.SharedKernel.Infrastructure.Cosmos.OutboxProcessing;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.SharedKernel.Infrastructure.MySql;
using Vulthil.SharedKernel.Infrastructure.MySql.OutboxProcessing;
using Vulthil.SharedKernel.Infrastructure.Npgsql;
using Vulthil.SharedKernel.Infrastructure.Npgsql.OutboxProcessing;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;
using Vulthil.xUnit;

namespace Vulthil.IntegrationTests;

/// <summary>
/// Pins the outbox-store selection contract of the provider <c>Use*</c> extensions: each selects its provider store
/// by default, and a store the user chose via <c>UseOutboxStore</c> is preserved no matter where in the chain the
/// provider extension is called. These are registration-shape tests — no database is contacted.
/// </summary>
public sealed class ProviderOutboxRegistrationTests : BaseUnitTestCase
{
    private const string ConnectionStringKey = "outbox-registration";
    private const string NpgsqlConnectionString = "Host=localhost;Database=registration;Username=registration";
    private const string MySqlConnectionString = "Server=localhost;Database=registration;User ID=registration";
    private const string CosmosConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=dGVzdA==;Database=registration";

    [Fact]
    public void UseNpgsqlSelectsTheNpgsqlStoreByDefault()
    {
        // Arrange
        var builder = NewBuilder(NpgsqlConnectionString);

        // Act
        builder.AddDbContext<RegistrationProbeDbContext>(database => database
            .UseNpgsql(ConnectionStringKey)
            .EnableOutboxProcessing());

        // Assert
        OutboxStoreDescriptor(builder).ImplementationType.ShouldBe(typeof(NpgsqlOutboxStore<RegistrationProbeDbContext>));
    }

    [Fact]
    public void UseNpgsqlPreservesAUserStoreSelectedBeforeIt()
    {
        // Arrange
        var builder = NewBuilder(NpgsqlConnectionString);

        // Act
        builder.AddDbContext<RegistrationProbeDbContext>(database => database
            .UseOutboxStore<CustomOutboxStore>()
            .UseNpgsql(ConnectionStringKey)
            .EnableOutboxProcessing());

        // Assert
        OutboxStoreDescriptor(builder).ImplementationType.ShouldBe(typeof(CustomOutboxStore));
    }

    [Fact]
    public void UseNpgsqlPreservesAUserStoreSelectedAfterIt()
    {
        // Arrange
        var builder = NewBuilder(NpgsqlConnectionString);

        // Act
        builder.AddDbContext<RegistrationProbeDbContext>(database => database
            .EnableOutboxProcessing()
            .UseNpgsql(ConnectionStringKey)
            .UseOutboxStore<CustomOutboxStore>());

        // Assert
        OutboxStoreDescriptor(builder).ImplementationType.ShouldBe(typeof(CustomOutboxStore));
    }

    [Fact]
    public void UseMySqlSelectsTheMySqlStoreByDefault()
    {
        // Arrange
        var builder = NewBuilder(MySqlConnectionString);

        // Act
        builder.AddDbContext<RegistrationProbeDbContext>(database => database
            .UseMySql(ConnectionStringKey)
            .EnableOutboxProcessing());

        // Assert
        OutboxStoreDescriptor(builder).ImplementationType.ShouldBe(typeof(MySqlOutboxStore<RegistrationProbeDbContext>));
    }

    [Fact]
    public void UseMySqlPreservesAUserStoreSelectedBeforeIt()
    {
        // Arrange
        var builder = NewBuilder(MySqlConnectionString);

        // Act
        builder.AddDbContext<RegistrationProbeDbContext>(database => database
            .UseOutboxStore<CustomOutboxStore>()
            .UseMySql(ConnectionStringKey)
            .EnableOutboxProcessing());

        // Assert
        OutboxStoreDescriptor(builder).ImplementationType.ShouldBe(typeof(CustomOutboxStore));
    }

    [Fact]
    public void UseMySqlPreservesAUserStoreSelectedAfterIt()
    {
        // Arrange
        var builder = NewBuilder(MySqlConnectionString);

        // Act
        builder.AddDbContext<RegistrationProbeDbContext>(database => database
            .EnableOutboxProcessing()
            .UseMySql(ConnectionStringKey)
            .UseOutboxStore<CustomOutboxStore>());

        // Assert
        OutboxStoreDescriptor(builder).ImplementationType.ShouldBe(typeof(CustomOutboxStore));
    }

    [Fact]
    public void UseCosmosDbSelectsTheCosmosStoreByDefault()
    {
        // Arrange
        var builder = NewBuilder(CosmosConnectionString);

        // Act
        builder.AddDbContext<RegistrationProbeDbContext>(database => database
            .UseCosmosDb(ConnectionStringKey)
            .EnableOutboxProcessing());

        // Assert
        OutboxStoreDescriptor(builder).ImplementationType.ShouldBe(typeof(CosmosOutboxStore<RegistrationProbeDbContext>));
    }

    [Fact]
    public void UseCosmosDbPreservesAUserStoreSelectedBeforeIt()
    {
        // Arrange
        var builder = NewBuilder(CosmosConnectionString);

        // Act
        builder.AddDbContext<RegistrationProbeDbContext>(database => database
            .UseOutboxStore<CustomOutboxStore>()
            .UseCosmosDb(ConnectionStringKey)
            .EnableOutboxProcessing());

        // Assert
        OutboxStoreDescriptor(builder).ImplementationType.ShouldBe(typeof(CustomOutboxStore));
    }

    [Fact]
    public void UseCosmosDbPreservesAUserStoreSelectedAfterIt()
    {
        // Arrange
        var builder = NewBuilder(CosmosConnectionString);

        // Act
        builder.AddDbContext<RegistrationProbeDbContext>(database => database
            .EnableOutboxProcessing()
            .UseCosmosDb(ConnectionStringKey)
            .UseOutboxStore<CustomOutboxStore>());

        // Assert
        OutboxStoreDescriptor(builder).ImplementationType.ShouldBe(typeof(CustomOutboxStore));
    }

    [Fact]
    public void UseCosmosDbThrowsWhenConfiguratorIsNull()
    {
        // Arrange
        IDatabaseInfrastructureConfigurator<RegistrationProbeDbContext> configurator = null!;

        // Act
        var act = () => configurator.UseCosmosDb(ConnectionStringKey);

        // Assert
        Should.Throw<ArgumentNullException>(act);
    }

    private static HostApplicationBuilder NewBuilder(string connectionString)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration[$"ConnectionStrings:{ConnectionStringKey}"] = connectionString;
        return builder;
    }

    private static ServiceDescriptor OutboxStoreDescriptor(HostApplicationBuilder builder) =>
        builder.Services.Single(descriptor => descriptor.ServiceType == typeof(IOutboxStore));

    internal sealed class RegistrationProbeDbContext(DbContextOptions<RegistrationProbeDbContext> options) : BaseDbContext(options)
    {
        protected override Assembly? ConfigurationAssembly => null;
    }

    internal sealed class CustomOutboxStore(RegistrationProbeDbContext dbContext, TimeProvider timeProvider, IOptions<OutboxProcessingOptions> options)
        : EntityFrameworkOutboxStore<RegistrationProbeDbContext>(dbContext, timeProvider, options);
}
