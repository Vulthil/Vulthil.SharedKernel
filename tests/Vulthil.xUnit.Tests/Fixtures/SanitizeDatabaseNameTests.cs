using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Respawn;
using Testcontainers.PostgreSql;
using Vulthil.xUnit.Fixtures;
using Xunit.Sdk;

namespace Vulthil.xUnit.Tests.Fixtures;

public sealed class SanitizeDatabaseNameTests : BaseUnitTestCase
{
    [Theory]
    [InlineData("myscope", "myscope")]
    [InlineData("MyScope", "myscope")]
    [InlineData("my-scope.db", "my_scope_db")]
    [InlineData("123abc", "db_123abc")]
    [InlineData("-abc", "db__abc")]
    [InlineData("a b", "a_b")]
    public void NormalizesScopeIdsAccordingToEngineNamingRules(string scopeId, string expected)
    {
        // Act
        var sanitized = FakeDatabaseFixture.SanitizeName(scopeId);

        // Assert
        sanitized.ShouldBe(expected);
    }

    [Fact]
    public void ANameOfExactlySixtyThreeCharactersIsReturnedUnchanged()
    {
        // Arrange
        var scopeId = new string('a', 63);

        // Act
        var sanitized = FakeDatabaseFixture.SanitizeName(scopeId);

        // Assert
        sanitized.ShouldBe(scopeId);
    }

    [Fact]
    public void ANameLongerThanSixtyThreeCharactersIsTruncated()
    {
        // Arrange
        var scopeId = new string('a', 100);

        // Act
        var sanitized = FakeDatabaseFixture.SanitizeName(scopeId);

        // Assert
        sanitized.Length.ShouldBe(63);
        sanitized.ShouldBe(new string('a', 63));
    }

    [Fact]
    public void APrefixedNameLongerThanSixtyThreeCharactersIsTruncatedAfterThePrefixIsAdded()
    {
        // Arrange
        var scopeId = "1" + new string('a', 65);

        // Act
        var sanitized = FakeDatabaseFixture.SanitizeName(scopeId);

        // Assert
        sanitized.Length.ShouldBe(63);
        sanitized.ShouldStartWith("db_1");
    }

    [Fact]
    public void NullScopeIdThrowsArgumentNullException()
    {
        // Act
        var exception = Should.Throw<ArgumentNullException>(() => FakeDatabaseFixture.SanitizeName(null!));

        // Assert
        exception.ShouldNotBeNull();
    }

    [Fact]
    public void EmptyScopeIdThrowsArgumentException()
    {
        // Act
        var exception = Should.Throw<ArgumentException>(() => FakeDatabaseFixture.SanitizeName(string.Empty));

        // Assert
        exception.ShouldNotBeNull();
    }

    // Never instantiated: only exists so SanitizeDatabaseName (protected static) can be called through a concrete
    // closed-generic subclass. PostgreSqlBuilder/PostgreSqlContainer are used purely as generic-constraint
    // witnesses — no container or Docker daemon is ever involved.
    public sealed class FakeDatabaseFixture(IMessageSink messageSink)
        : TestDatabaseContainerFixture<FakeDbContext, PostgreSqlBuilder, PostgreSqlContainer>(messageSink)
    {
        protected override PostgreSqlBuilder Configure() => throw new NotSupportedException();

        protected override IDbAdapter DbAdapter => throw new NotSupportedException();

        public override DbProviderFactory DbProviderFactory => throw new NotSupportedException();

        public override string ConnectionStringKey => throw new NotSupportedException();

        public static string SanitizeName(string scopeId) => SanitizeDatabaseName(scopeId);
    }

    public sealed class FakeDbContext : DbContext;
}
