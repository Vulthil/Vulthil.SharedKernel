using FluentAssertions;
using Vulthil.SharedKernel.Events;
using Vulthil.SharedKernel.Primitives;

namespace Vulthil.SharedKernel.Tests;
public sealed class EntityTests
{
    private sealed record TestEntityId(Guid Value);
    private sealed record TestEntityEvent(Guid Id) : IDomainEvent;
    private sealed class TestEntity : Entity<TestEntityId>
    {
        private TestEntity(TestEntityId testEntityId) : base(testEntityId) { }

        public static TestEntity Create() => new(new(Guid.NewGuid()));
        public void RaiseEvent() => Raise(new TestEntityEvent(Id.Value));
    }


    [Fact]
    public void EntityShouldBeConstructable()
    {
        // Act
        var testEntity = TestEntity.Create();

        // Assert
        testEntity.Id.Should().NotBe(default(TestEntityId));
        testEntity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void EntityShouldBeAbleToRaiseEvents()
    {
        // Arrange
        var testEntity = TestEntity.Create();

        // Act
        testEntity.RaiseEvent();

        // Assert
        testEntity.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TestEntityEvent>()
                .Which.Id.Should().Be(testEntity.Id.Value);
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
        testEntity.DomainEvents.Should().BeEmpty();
    }
}
