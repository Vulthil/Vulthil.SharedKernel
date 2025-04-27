using Vulthil.Framework.Results;
using Vulthil.Framework.Results.Results;
using Vulthil.SharedKernel.xUnit;

namespace Vulthil.SharedKernel.Tests.Core.Results;

public sealed class ResultTests : BaseUnitTestCase
{
    [Fact]
    public void ResultShouldBeSuccess()
    {
        // Act
        var result = Result.Success();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Error.ShouldBe(Error.None);
    }

    [Fact]
    public void ResultInternalConstructorShouldThrowIfSuccessAndError()
    {
        // Act
        Func<Result> act = () => new Result(true, Error.NullValue);

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Theory]
    [InlineData(["string"])]
    [InlineData([1])]
    public void ResultShouldBeSuccessWithValue<T>(T value)
    {
        // Act
        var result = Result.Success(value);
        Result<T> implicitOperator = value;

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(value);
        result.IsFailure.ShouldBeFalse();
        result.Error.ShouldBe(Error.None);

        implicitOperator.IsSuccess.ShouldBeTrue();
        implicitOperator.Value.ShouldBe(value);
        implicitOperator.IsFailure.ShouldBeFalse();
        implicitOperator.Error.ShouldBe(Error.None);
    }

    [Fact]
    public void ResultShouldBeFailure()
    {
        // Act
        var result = Result.Failure(Error.NullValue);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(Error.NullValue);
        result.Error.Code.ShouldBe(Error.NullValue.Code);
        result.Error.Description.ShouldBe(Error.NullValue.Description);
        result.Error.Type.ShouldBe(Error.NullValue.Type);
    }

    [Fact]
    public void ResultShouldThrowIfFailureIsNone()
    {
        // Act
        Action act = () => Result.Failure(Error.None);

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void ResultShouldThrowFailureWithValue()
    {
        // Act
        var result = Result.Failure<Result>(Error.NullValue);
        Result? nullValue = null;
        Result<Result> implicitOperator = nullValue;

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(Error.NullValue);
        Should.Throw<InvalidOperationException>(() => result.Value);

        implicitOperator.IsSuccess.ShouldBeFalse();
        implicitOperator.IsFailure.ShouldBeTrue();
        implicitOperator.Error.ShouldBe(Error.NullValue);
        Should.Throw<InvalidOperationException>(() => implicitOperator.Value);
    }

    [Fact]
    public void ResultShouldReturnValidationError()
    {
        var validationError = new ValidationError([Error.NullValue]);
        // Act
        var result = Result.ValidationFailure<Result>(validationError);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>()
            .Errors.ShouldHaveSingleItem()
            .ShouldBe(Error.NullValue);
        Should.Throw<InvalidOperationException>(() => result.Value);
    }
}
