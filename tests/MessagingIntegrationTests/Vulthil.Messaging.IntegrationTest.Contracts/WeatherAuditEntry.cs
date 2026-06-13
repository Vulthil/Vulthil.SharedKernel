namespace Vulthil.Messaging.IntegrationTest.Contracts;

public sealed record WeatherAuditEntry(Guid SourceId, string Location);
