using Vulthil.SharedKernel.Exceptions;
using Vulthil.SharedKernel.Primitives;
using Vulthil.SharedKernel.xUnit;

namespace Vulthil.SharedKernel.Tests.Core;

public sealed class DomainExceptionTests : BaseUnitTestCase
{
    private sealed class TestDomainException(Error error) : DomainException(error);

    [Fact]
    public void DomainExceptionShouldBeConstructable()
    {
        // Act
        var exception = new TestDomainException(Error.NullValue);

        // Assert
        exception.Error.ShouldBe(Error.NullValue);
        exception.Message.ShouldBe(Error.NullValue.Description);
    }
}
