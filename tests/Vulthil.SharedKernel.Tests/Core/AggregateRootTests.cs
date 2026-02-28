using Vulthil.SharedKernel.Events;
using Vulthil.SharedKernel.Primitives;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Tests.Core;

/// <summary>
/// Represents the AggregateRootTests.
/// </summary>
public sealed class AggregateRootTests : BaseUnitTestCase
{
    private sealed record TestEntityId(Guid Value);
    private sealed record TestEntityEvent(Guid Id) : IDomainEvent;
    private sealed class TestEntity : AggregateRoot<TestEntityId>
    {
        private TestEntity(TestEntityId testEntityId) : base(testEntityId)
        {
        }


        /// <summary>
        /// Executes this member.
        /// </summary>
        public static TestEntity Create() => new(new(Guid.NewGuid()));
        /// <summary>
        /// Executes this member.
        /// </summary>
        public void RaiseEvent() => Raise(new TestEntityEvent(Id.Value));
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void EntityShouldBeConstructable()
    {
        // Act
        var testEntity = TestEntity.Create();

        // Assert
        testEntity.Id.ShouldNotBe(default);
        testEntity.DomainEvents.ShouldBeEmpty();
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
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

    /// <summary>
    /// Executes this member.
    /// </summary>
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
