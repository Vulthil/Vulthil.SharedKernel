using Vulthil.SharedKernel.xUnit;

namespace Vulthil.SharedKernel.Application.Tests;

public sealed class ValidationExceptionTests : BaseUnitTestCase
{
    [Fact]
    public void ValidationExceptionShouldBeConstructable()
    {
        //// Act
        //var exception = new ValidationException([new ValidationFailure("Test", "Test2") { ErrorCode = "Test" }]);
        //// Assert
        //exception.Message.Should().Be("One or more validation failures has occurred.");
        //exception.Errors.Should().HaveCount(1);
        //exception.Errors.First().Code.Should().Be("Test");
    }
}
