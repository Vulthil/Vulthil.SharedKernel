using FluentAssertions;
using Vulthil.SharedKernel.Exceptions;
using Vulthil.SharedKernel.Primitives;
using Vulthil.SharedKernel.xUnit;

namespace Vulthil.SharedKernel.Tests;

public sealed class DomainExceptionTests : BaseUnitTestCase
{
    private sealed class TestDomainException(Error error) : DomainException(error);

    [Fact]
    public void DomainExceptionShouldBeConstructable()
    {
        // Act
        var exception = new TestDomainException(Error.NullValue);

        // Assert
        exception.Error.Should().Be(Error.NullValue);
        exception.Message.Should().Be(Error.NullValue.Description);
    }
}
