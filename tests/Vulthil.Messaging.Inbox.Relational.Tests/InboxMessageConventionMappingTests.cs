using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Vulthil.Messaging.Inbox.EntityFrameworkCore;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Inbox.Relational.Tests;

/// <summary>
/// Pins that <see cref="InboxMessage"/> maps validly by convention alone: a context that exposes the set but never
/// calls <c>ApplyRelationalInbox</c> gets the same bounded <c>MessageId</c> key the extension configures, instead of
/// failing model validation on its first use.
/// </summary>
public sealed class InboxMessageConventionMappingTests : BaseUnitTestCase
{
    [Fact]
    public void MessageIdIsThePrimaryKeyWithoutTheRelationalMapping()
    {
        // Arrange
        using var context = new ConventionOnlyDbContext(SqliteOptions<ConventionOnlyDbContext>());

        // Act
        var key = InboxEntity(context).FindPrimaryKey();

        // Assert
        key.ShouldNotBeNull();
        key.Properties.Single().Name.ShouldBe(nameof(InboxMessage.MessageId));
    }

    [Fact]
    public void MessageIdIsBoundedWithoutTheRelationalMapping()
    {
        // Arrange
        using var context = new ConventionOnlyDbContext(SqliteOptions<ConventionOnlyDbContext>());

        // Act
        var messageId = InboxEntity(context).FindProperty(nameof(InboxMessage.MessageId));

        // Assert
        messageId.ShouldNotBeNull();
        messageId.GetMaxLength().ShouldBe(256);
        messageId.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void ConventionMappingMatchesTheRelationalMapping()
    {
        // Arrange
        using var conventionOnly = new ConventionOnlyDbContext(SqliteOptions<ConventionOnlyDbContext>());
        using var configured = new ConfiguredDbContext(SqliteOptions<ConfiguredDbContext>());

        // Act
        var conventionShape = Shape(InboxEntity(conventionOnly));
        var configuredShape = Shape(InboxEntity(configured));

        // Assert
        conventionShape.ShouldBe(configuredShape);
    }

    private static IEntityType InboxEntity(DbContext context) =>
        context.Model.FindEntityType(typeof(InboxMessage)) ?? throw new InvalidOperationException("InboxMessage is not mapped.");

    private static (string Key, int? MaxLength, bool Nullable, string Table) Shape(IEntityType entityType) =>
        (entityType.FindPrimaryKey()!.Properties.Single().Name,
         entityType.FindProperty(nameof(InboxMessage.MessageId))!.GetMaxLength(),
         entityType.FindProperty(nameof(InboxMessage.MessageId))!.IsNullable,
         entityType.GetTableName()!);

    private static DbContextOptions<TContext> SqliteOptions<TContext>()
        where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>().UseSqlite("Data Source=:memory:").Options;

    public sealed class ConventionOnlyDbContext(DbContextOptions<ConventionOnlyDbContext> options) : DbContext(options), ISaveInboxMessages
    {
        public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    }

    public sealed class ConfiguredDbContext(DbContextOptions<ConfiguredDbContext> options) : DbContext(options), ISaveInboxMessages
    {
        public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyRelationalInbox();
    }
}
