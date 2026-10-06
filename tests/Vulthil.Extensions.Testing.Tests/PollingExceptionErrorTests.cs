using Vulthil.Results;
using Vulthil.xUnit;

namespace Vulthil.Extensions.Testing.Tests;

public sealed class PollingExceptionErrorTests : BaseUnitTestCase
{
    [Fact]
    public void KeepsTheExceptionAndUsesItsMessageAsAFailure()
    {
        // Arrange
        var exception = new HttpRequestException("Connection refused");

        // Act
        var error = new PollingExceptionError(exception);

        // Assert
        error.Exception.ShouldBeSameAs(exception);
        error.Code.ShouldBe("Polling.Exception");
        error.Description.ShouldBe("Connection refused");
        error.Type.ShouldBe(ErrorType.Failure);
    }

    [Fact]
    public void NullExceptionThrowsArgumentNullException()
    {
        // Arrange
        Exception? exception = null;

        // Act
        var thrown = Should.Throw<ArgumentNullException>(() => new PollingExceptionError(exception!));

        // Assert
        thrown.ParamName.ShouldBe("Exception");
    }
}
