namespace Vulthil.Messaging.IntegrationTest.Contracts;

public sealed record FlakyCommand(Guid Id, int FailUntilAttempt);
