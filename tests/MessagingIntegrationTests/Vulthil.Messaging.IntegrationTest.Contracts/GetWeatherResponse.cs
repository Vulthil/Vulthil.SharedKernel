namespace Vulthil.Messaging.IntegrationTest.Contracts;

public sealed record GetWeatherResponse(string Location, int TemperatureC, DateTimeOffset RecordedAt);
