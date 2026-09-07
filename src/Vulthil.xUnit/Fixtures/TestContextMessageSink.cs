using Xunit.Sdk;

namespace Vulthil.xUnit.Fixtures;

// Testcontainers' fixture bases demand an IMessageSink, but xUnit only exposes the diagnostic sink through
// TestContext.Current.SendDiagnosticMessage; this adapter bridges the two so fixtures need nothing injected.
internal sealed class TestContextMessageSink : IMessageSink
{
    public static TestContextMessageSink Instance { get; } = new();

    private TestContextMessageSink()
    {
    }

    public bool OnMessage(IMessageSinkMessage message)
    {
        if (message is IDiagnosticMessage diagnostic)
        {
            TestContext.Current.SendDiagnosticMessage(diagnostic.Message);
        }

        return true;
    }
}
