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
/// Pins the outbox-store selection contract: a provider <c>Use*</c> extension proposes its store through
/// <c>UseDefaultOutboxStore</c>, a store the user chose via <c>UseOutboxStore</c> wins no matter where in the chain
/// either call sits, and with neither the open Entity Framework store is closed over the context. These are
/// registration-shape tests — no database is contacted.
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
    public void UseDefaultOutboxStoreAppliesWhenNoStoreWasSelected()
    {
        // Arrange
        var builder = NewBuilder();

        // Act
        builder.AddDbContext<RegistrationProbeDbContext>(database => database
            .UseDefaultOutboxStore<CustomOutboxStore>()
            .EnableOutboxProcessing());

        // Assert
        OutboxStoreDescriptor(builder).ImplementationType.ShouldBe(typeof(CustomOutboxStore));
    }

    [Fact]
    public void UseDefaultOutboxStoreYieldsToAStoreSelectedBeforeIt()
    {
        // Arrange
        var builder = NewBuilder();

        // Act
        builder.AddDbContext<RegistrationProbeDbContext>(database => database
            .UseOutboxStore<CustomOutboxStore>()
            .UseDefaultOutboxStore<OtherOutboxStore>()
            .EnableOutboxProcessing());

        // Assert
        OutboxStoreDescriptor(builder).ImplementationType.ShouldBe(typeof(CustomOutboxStore));
    }

    [Fact]
    public void UseDefaultOutboxStoreYieldsToAStoreSelectedAfterIt()
    {
        // Arrange
        var builder = NewBuilder();

        // Act
        builder.AddDbContext<RegistrationProbeDbContext>(database => database
            .UseDefaultOutboxStore<OtherOutboxStore>()
            .UseOutboxStore<CustomOutboxStore>()
            .EnableOutboxProcessing());

        // Assert
        OutboxStoreDescriptor(builder).ImplementationType.ShouldBe(typeof(CustomOutboxStore));
    }

    [Fact]
    public void WithoutAProviderOrASelectionTheEntityFrameworkStoreIsClosedOverTheContext()
    {
        // Arrange
        var builder = NewBuilder();

        // Act
        builder.AddDbContext<RegistrationProbeDbContext>(database => database.EnableOutboxProcessing());

        // Assert
        OutboxStoreDescriptor(builder).ImplementationType.ShouldBe(typeof(EntityFrameworkOutboxStore<RegistrationProbeDbContext>));
    }

    [Theory]
    [InlineData("Select,Provider,Enable")]
    [InlineData("Select,Enable,Provider")]
    [InlineData("Provider,Select,Enable")]
    [InlineData("Provider,Enable,Select")]
    [InlineData("Enable,Select,Provider")]
    [InlineData("Enable,Provider,Select")]
    public void AUserStoreWinsInEveryChainOrder(string chain)
    {
        // Arrange
        var builder = NewBuilder(NpgsqlConnectionString);

        // Act
        builder.AddDbContext<RegistrationProbeDbContext>(database => ApplyChain(database, chain));

        // Assert
        OutboxStoreDescriptor(builder).ImplementationType.ShouldBe(typeof(CustomOutboxStore));
    }

    [Theory]
    [InlineData("Provider,Enable")]
    [InlineData("Enable,Provider")]
    public void TheProviderStoreIsTheDefaultInEveryChainOrder(string chain)
    {
        // Arrange
        var builder = NewBuilder(NpgsqlConnectionString);

        // Act
        builder.AddDbContext<RegistrationProbeDbContext>(database => ApplyChain(database, chain));

        // Assert
        OutboxStoreDescriptor(builder).ImplementationType.ShouldBe(typeof(NpgsqlOutboxStore<RegistrationProbeDbContext>));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AUserStoreSelectedInsideAnOnConfiguredCallbackWinsWhereverTheCallbackIsRegistered(bool callbackBeforeProvider)
    {
        // Arrange
        var builder = NewBuilder(NpgsqlConnectionString);

        // Act
        builder.AddDbContext<RegistrationProbeDbContext>(database =>
        {
            if (callbackBeforeProvider)
            {
                database.OnConfigured(c => c.UseOutboxStore<CustomOutboxStore>());
            }

            database.UseNpgsql(ConnectionStringKey).EnableOutboxProcessing();

            if (!callbackBeforeProvider)
            {
                database.OnConfigured(c => c.UseOutboxStore<CustomOutboxStore>());
            }
        });

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

    private static HostApplicationBuilder NewBuilder(string? connectionString = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        if (connectionString is not null)
        {
            builder.Configuration[$"ConnectionStrings:{ConnectionStringKey}"] = connectionString;
        }

        return builder;
    }

    private static ServiceDescriptor OutboxStoreDescriptor(HostApplicationBuilder builder) =>
        builder.Services.Single(descriptor => descriptor.ServiceType == typeof(IOutboxStore));

    private static void ApplyChain(IDatabaseInfrastructureConfigurator<RegistrationProbeDbContext> database, string chain)
    {
        foreach (var step in chain.Split(','))
        {
            _ = step switch
            {
                "Provider" => database.UseNpgsql(ConnectionStringKey),
                "Enable" => database.EnableOutboxProcessing(),
                "Select" => database.UseOutboxStore<CustomOutboxStore>(),
                _ => throw new ArgumentOutOfRangeException(nameof(chain), step, "Unknown chain step."),
            };
        }
    }

    internal sealed class RegistrationProbeDbContext(DbContextOptions<RegistrationProbeDbContext> options) : BaseDbContext(options)
    {
        protected override Assembly? ConfigurationAssembly => null;
    }

    internal sealed class CustomOutboxStore(RegistrationProbeDbContext dbContext, TimeProvider timeProvider, IOptions<OutboxProcessingOptions> options)
        : EntityFrameworkOutboxStore<RegistrationProbeDbContext>(dbContext, timeProvider, options);

    internal sealed class OtherOutboxStore(RegistrationProbeDbContext dbContext, TimeProvider timeProvider, IOptions<OutboxProcessingOptions> options)
        : EntityFrameworkOutboxStore<RegistrationProbeDbContext>(dbContext, timeProvider, options);
}
