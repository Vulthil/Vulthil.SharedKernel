using FluentAssertions;
using Vulthil.SharedKernel.Primitives;
using Vulthil.SharedKernel.xUnit;

namespace Vulthil.SharedKernel.Tests;

public sealed class ResultTests : BaseUnitTestCase
{
    [Fact]
    public void ResultShouldBeSuccess()
    {
        // Act
        var result = Result.Success();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void ResultInternalConstructorShouldThrowIfSuccessAndError()
    {
        // Act
        Func<Result> act = () => new Result(true, Error.NullValue);

        // Assert
        act.Should().ThrowExactly<ArgumentException>();
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
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);

        implicitOperator.IsSuccess.Should().BeTrue();
        implicitOperator.Value.Should().Be(value);
        implicitOperator.IsFailure.Should().BeFalse();
        implicitOperator.Error.Should().Be(Error.None);
    }

    [Fact]
    public void ResultShouldBeFailure()
    {
        // Act
        var result = Result.Failure(Error.NullValue);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.NullValue);
        result.Error.Code.Should().Be(Error.NullValue.Code);
        result.Error.Description.Should().Be(Error.NullValue.Description);
        result.Error.Type.Should().Be(Error.NullValue.Type);
    }

    [Fact]
    public void ResultShouldThrowIfFailureIsNone()
    {
        // Act
        Action act = () => Result.Failure(Error.None);

        // Assert
        act.Should().ThrowExactly<ArgumentException>();
    }

    [Fact]
    public void ResultShouldThrowFailureWithValue()
    {
        // Act
        var result = Result.Failure<Result>(Error.NullValue);
        Result? nullValue = null;
        Result<Result> implicitOperator = nullValue;

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.NullValue);
        result.Invoking(r => r.Value).Should().ThrowExactly<InvalidOperationException>();

        implicitOperator.IsSuccess.Should().BeFalse();
        implicitOperator.IsFailure.Should().BeTrue();
        implicitOperator.Error.Should().Be(Error.NullValue);
        implicitOperator.Invoking(r => r.Value).Should().ThrowExactly<InvalidOperationException>();
    }

    [Fact]
    public void ResultShouldReturnValidationError()
    {
        var validationError = new ValidationError([Error.NullValue]);
        // Act
        var result = Result.ValidationFailure<Result>(validationError);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ValidationError>()
            .Which.Errors.Should().HaveCount(1)
                .And.Contain(Error.NullValue);
        result.Invoking(r => r.Value).Should().ThrowExactly<InvalidOperationException>();
    }

    public static TheoryData<Error, ErrorType> TestData => new()
    {
        { Error.NotFound("C", "D"), ErrorType.NotFound },
        { Error.Validation("C", "D"), ErrorType.Validation },
        { Error.Conflict("C", "D"), ErrorType.Conflict },
        { Error.Failure("C", "D"), ErrorType.Failure },
    };

    [Theory]
    [MemberData(nameof(TestData))]
    public void ErrorStaticFactoryMethodsShouldCreateErrors(Error error, ErrorType expectedErrorType) =>
        // Assert
        error.Type.Should().Be(expectedErrorType);
}
