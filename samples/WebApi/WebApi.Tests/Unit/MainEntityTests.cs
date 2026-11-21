using Shouldly;
using Vulthil.xUnit;
using WebApi.Domain.MainEntities;
using WebApi.Domain.MainEntities.Events;

namespace WebApi.Tests.Unit;

// The alias sits inside the namespace so it wins over the WebApi.MainEntity namespace of the endpoints.
using MainEntity = WebApi.Domain.MainEntities.MainEntity;

public sealed class MainEntityTests : BaseUnitTestCase
{
    [Fact]
    public void Create_trims_the_name_and_raises_MainEntityCreatedEvent()
    {
        // Act
        var result = MainEntity.Create("  Widget ");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Widget");
        result.Value.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<MainEntityCreatedEvent>();
    }

    [Fact]
    public void Create_with_a_too_long_name_fails()
    {
        // Act
        var result = MainEntity.Create(new string('x', MainEntity.MaxNameLength + 1));

        // Assert
        result.Error.ShouldBe(MainEntityErrors.NameTooLong);
    }

    [Fact]
    public void Rename_to_the_same_name_fails_without_an_event()
    {
        // Arrange
        var mainEntity = MainEntity.Create("Widget").Value;
        mainEntity.ClearDomainEvents();

        // Act
        var result = mainEntity.Rename("Widget");

        // Assert
        result.Error.ShouldBe(MainEntityErrors.NameUnchanged);
        mainEntity.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Rename_to_a_reserved_name_throws_a_domain_exception_with_a_conflict_error()
    {
        // Arrange
        var mainEntity = MainEntity.Create("Widget").Value;

        // Act
        var exception = Should.Throw<ReservedMainEntityNameException>(() => mainEntity.Rename("Admin"));

        // Assert
        exception.Error.Type.ShouldBe(Vulthil.Results.ErrorType.Conflict);
        mainEntity.Name.ShouldBe("Widget");
    }
}
