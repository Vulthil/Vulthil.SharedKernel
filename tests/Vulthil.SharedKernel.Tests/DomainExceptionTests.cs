using FluentAssertions;
using Vulthil.SharedKernel.Exceptions;
using Vulthil.SharedKernel.Primitives;

namespace Vulthil.SharedKernel.Tests;
public sealed class DomainExceptionTests
{
    private sealed class TestDomainException : DomainException
    {
        public TestDomainException(Error error) : base(error)
        {
        }
    }

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
