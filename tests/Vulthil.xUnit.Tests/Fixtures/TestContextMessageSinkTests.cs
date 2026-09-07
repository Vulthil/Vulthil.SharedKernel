using Vulthil.xUnit.Fixtures;
using Xunit.Sdk;

namespace Vulthil.xUnit.Tests.Fixtures;

public sealed class TestContextMessageSinkTests : BaseUnitTestCase
{
    private static TestContextMessageSink Target => TestContextMessageSink.Instance;

    [Fact]
    public void ForwardingADiagnosticMessageKeepsTheSenderRunning()
    {
        // Arrange
        var message = Mock.Of<IDiagnosticMessage>(m => m.Message == "[testcontainers.org] Docker container started");

        // Act
        var shouldContinue = Target.OnMessage(message);

        // Assert
        shouldContinue.ShouldBeTrue();
    }

    [Fact]
    public void ANonDiagnosticMessageIsIgnoredAndKeepsTheSenderRunning()
    {
        // Arrange
        var message = Mock.Of<IMessageSinkMessage>();

        // Act
        var shouldContinue = Target.OnMessage(message);

        // Assert
        shouldContinue.ShouldBeTrue();
    }
}
