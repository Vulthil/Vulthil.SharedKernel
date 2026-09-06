using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Vulthil.SharedKernel.Infrastructure.Cosmos;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.SharedKernel.Infrastructure.MySql;
using Vulthil.SharedKernel.Infrastructure.Npgsql;
using Vulthil.SharedKernel.Outbox;
using Vulthil.xUnit;

namespace Vulthil.IntegrationTests;

/// <summary>
/// Pins the model each provider's <c>Apply*Outbox</c> extension produces: the provider-agnostic key and required
/// columns, plus the provider's own column types and indexes. The models are built offline — no database is
/// contacted.
/// </summary>
public sealed class ProviderOutboxMappingTests : BaseUnitTestCase
{
    [Fact]
    public void ApplyNpgsqlOutboxAddsJsonbContentAndAFilteredPendingIndexToTheAgnosticMapping()
    {
        // Arrange
        using var context = new NpgsqlMappedDbContext(
            new DbContextOptionsBuilder<NpgsqlMappedDbContext>().UseNpgsql("Host=localhost;Database=mapping").Options);

        // Act
        var entity = context.Model.FindEntityType(typeof(OutboxMessage)).ShouldNotBeNull();

        // Assert
        AssertAgnosticMapping(entity);
        entity.FindProperty(nameof(OutboxMessage.Content))!.GetColumnType().ShouldBe("jsonb");
        var index = entity.GetIndexes().ShouldHaveSingleItem();
        index.GetDatabaseName().ShouldBe("IX_OutboxMessages_OccurredOnUtc_Id");
        index.Properties.Select(property => property.Name).ShouldBe([nameof(OutboxMessage.OccurredOnUtc), nameof(OutboxMessage.Id)]);
        index.GetFilter().ShouldBe("\"ProcessedOnUtc\" IS NULL AND \"FailedOnUtc\" IS NULL");
    }

    [Fact]
    public void ApplyMySqlOutboxAddsLongtextContentABoundedTypeAndAPendingIndexToTheAgnosticMapping()
    {
        // Arrange
        using var context = new MySqlMappedDbContext(
            new DbContextOptionsBuilder<MySqlMappedDbContext>().UseMySql("Server=localhost;Database=mapping", ServerVersion.Parse("8.0.36-mysql")).Options);

        // Act
        var entity = context.Model.FindEntityType(typeof(OutboxMessage)).ShouldNotBeNull();

        // Assert
        AssertAgnosticMapping(entity);
        entity.FindProperty(nameof(OutboxMessage.Type))!.GetMaxLength().ShouldBe(256);
        entity.FindProperty(nameof(OutboxMessage.Content))!.GetColumnType().ShouldBe("longtext");
        var index = entity.GetIndexes().ShouldHaveSingleItem();
        index.GetDatabaseName().ShouldBe("IX_OutboxMessages_Pending");
        index.Properties.Select(property => property.Name).ShouldBe(
            [nameof(OutboxMessage.ProcessedOnUtc), nameof(OutboxMessage.FailedOnUtc), nameof(OutboxMessage.OccurredOnUtc), nameof(OutboxMessage.Id)]);
        index.GetFilter().ShouldBeNull();
    }

    [Fact]
    public void ApplyCosmosOutboxIsTheAgnosticMapping()
    {
        // Arrange
        using var context = new CosmosMappedDbContext(
            new DbContextOptionsBuilder<CosmosMappedDbContext>().UseCosmos("https://localhost:8081/", "dGVzdA==", "mapping").Options);

        // Act
        var entity = context.Model.FindEntityType(typeof(OutboxMessage)).ShouldNotBeNull();

        // Assert
        AssertAgnosticMapping(entity);
        entity.GetIndexes().ShouldBeEmpty();
    }

    private static void AssertAgnosticMapping(IEntityType entity)
    {
        entity.FindPrimaryKey().ShouldNotBeNull().Properties.Select(property => property.Name).ShouldBe([nameof(OutboxMessage.Id)]);
        entity.FindProperty(nameof(OutboxMessage.Type))!.IsNullable.ShouldBeFalse();
        entity.FindProperty(nameof(OutboxMessage.Content))!.IsNullable.ShouldBeFalse();
    }

    internal sealed class NpgsqlMappedDbContext(DbContextOptions<NpgsqlMappedDbContext> options) : BaseDbContext(options)
    {
        protected override Assembly? ConfigurationAssembly => null;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyNpgsqlOutbox();
        }
    }

    internal sealed class MySqlMappedDbContext(DbContextOptions<MySqlMappedDbContext> options) : BaseDbContext(options)
    {
        protected override Assembly? ConfigurationAssembly => null;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyMySqlOutbox();
        }
    }

    internal sealed class CosmosMappedDbContext(DbContextOptions<CosmosMappedDbContext> options) : BaseDbContext(options)
    {
        protected override Assembly? ConfigurationAssembly => null;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyCosmosOutbox();
        }
    }
}
