namespace Vulthil.Messaging.IntegrationTest.Contracts;

public sealed record RecordWeatherCommand(Guid Id, string Location, int TemperatureC);
