using Vulthil.Results;
using Vulthil.SharedKernel.Exceptions;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Tests.Core;

/// <summary>
/// Represents the DomainExceptionTests.
/// </summary>
public sealed class DomainExceptionTests : BaseUnitTestCase
{
    private sealed class TestDomainException(Error error) : DomainException(error);

    /// <summary>
    /// Executes this member.
    /// </summary>
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
