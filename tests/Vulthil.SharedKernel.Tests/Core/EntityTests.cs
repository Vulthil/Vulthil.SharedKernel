using Vulthil.SharedKernel.Primitives;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Tests.Core;

public sealed class EntityTests : BaseUnitTestCase
{
    private readonly record struct TestId(Guid Value);
    private sealed class TestEntity(TestId id) : Entity<TestId>(id);
    private sealed class OtherEntity(TestId id) : Entity<TestId>(id);

    [Fact]
    public void EntitiesOfTheSameTypeWithEqualIdsAreEqual()
    {
        // Arrange
        var id = new TestId(Guid.NewGuid());
        var first = new TestEntity(id);
        var second = new TestEntity(id);

        // Assert
        first.Equals(second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void EntitiesWithDifferentIdsAreNotEqual()
    {
        // Arrange
        var first = new TestEntity(new TestId(Guid.NewGuid()));
        var second = new TestEntity(new TestId(Guid.NewGuid()));

        // Assert
        first.Equals(second).ShouldBeFalse();
    }

    [Fact]
    public void EntitiesOfDifferentTypesWithTheSameIdAreNotEqual()
    {
        // Arrange
        var id = new TestId(Guid.NewGuid());
        var entity = new TestEntity(id);
        var other = new OtherEntity(id);

        // Assert
        entity.Equals(other).ShouldBeFalse();
    }

    [Fact]
    public void TransientEntitiesAreOnlyEqualToThemselves()
    {
        // Arrange
        var first = new TestEntity(default);
        var second = new TestEntity(default);

        // Assert
        first.Equals(first).ShouldBeTrue();
        first.Equals(second).ShouldBeFalse();
    }

    [Fact]
    public void EqualsReturnsFalseForOtherObjectTypes()
    {
        // Arrange
        var entity = new TestEntity(new TestId(Guid.NewGuid()));

        // Assert
        entity.Equals("not an entity").ShouldBeFalse();
    }

    [Fact]
    public void EqualEntitiesDeduplicateInAHashSet()
    {
        // Arrange
        var id = new TestId(Guid.NewGuid());

        // Act
        var set = new HashSet<TestEntity> { new(id), new(id) };

        // Assert
        set.ShouldHaveSingleItem();
    }
}
