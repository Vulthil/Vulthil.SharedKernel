using Vulthil.SharedKernel.Events;
using Vulthil.SharedKernel.Primitives;
using Vulthil.SharedKernel.xUnit;

namespace Vulthil.SharedKernel.Tests.Core;

public sealed class AggregateRootTests : BaseUnitTestCase
{
    private sealed record TestEntityId(Guid Value);
    private sealed record TestEntityEvent(Guid Id) : IDomainEvent;
    private sealed class TestEntity(TestEntityId testEntityId) : AggregateRoot<TestEntityId>(testEntityId)
    {
        public static TestEntity Create() => new(new(Guid.NewGuid()));
        public void RaiseEvent() => Raise(new TestEntityEvent(Id.Value));
    }

    [Fact]
    public void EntityShouldBeConstructable()
    {
        // Act
        var testEntity = TestEntity.Create();

        // Assert
        testEntity.Id.ShouldNotBe(default);
        testEntity.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void EntityShouldBeAbleToRaiseEvents()
    {
        // Arrange
        var testEntity = TestEntity.Create();

        // Act
        testEntity.RaiseEvent();

        // Assert
        testEntity.DomainEvents.ShouldHaveSingleItem()
            .ShouldBeOfType<TestEntityEvent>()
                .Id.ShouldBe(testEntity.Id.Value);
    }

    [Fact]
    public void EntityShouldBeAbleToClearEvents()
    {
        // Arrange
        var testEntity = TestEntity.Create();
        testEntity.RaiseEvent();

        // Act
        testEntity.ClearDomainEvents();

        // Assert
        testEntity.DomainEvents.ShouldBeEmpty();
    }
}
