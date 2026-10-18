using FluentValidation.Results;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Behaviors;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Application.Tests.Behaviors;

public sealed class ValidationFailureConversionTests : BaseUnitTestCase
{
    [Fact]
    public void EachFailureBecomesAValidationErrorForItsPropertyWithItsCodeAndMessage()
    {
        // Arrange
        ValidationFailure[] failures =
        [
            new("Email", "'Email' must not be empty.") { ErrorCode = "NotEmptyValidator" },
            new("Address.Street", "Street is required.") { ErrorCode = "Address.StreetRequired" },
        ];

        // Act
        var validationError = ValidationFailureConversion.ToValidationError(failures);

        // Assert
        Assert.Collection(
            validationError.Errors,
            error => Assert.Equal(Error.Validation("NotEmptyValidator", "'Email' must not be empty.") with { Field = "Email" }, error),
            error => Assert.Equal(Error.Validation("Address.StreetRequired", "Street is required.") with { Field = "Address.Street" }, error));
    }

    [Fact]
    public void AFailureWithoutAPropertyNameGetsNoField()
    {
        // Arrange
        ValidationFailure[] failures = [new(string.Empty, "The order needs at least one line.") { ErrorCode = "Order.NoLines" }];

        // Act
        var validationError = ValidationFailureConversion.ToValidationError(failures);

        // Assert
        var error = Assert.Single(validationError.Errors);
        Assert.Null(error.Field);
        Assert.Equal("Order.NoLines", error.Code);
    }
}
