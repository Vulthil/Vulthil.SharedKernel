namespace Vulthil.SharedKernel.Messaging.RabbitMq;

public sealed record RabbitMqOptions
{
    public string ConnectionString { get; set; } = string.Empty;
}
