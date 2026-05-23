namespace Vulthil.Messaging.IntegrationTest.Contracts;

public sealed record WeatherUpdatedEvent(Guid Id, string Location, int TemperatureC);
