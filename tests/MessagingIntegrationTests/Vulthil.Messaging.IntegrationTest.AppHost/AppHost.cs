var builder = DistributedApplication.CreateBuilder(args);

var rabbitMq = builder.AddRabbitMQ("rabbitmq")
    .WithLifetime(ContainerLifetime.Session);

builder.AddDotnetProject("producer", "../Vulthil.Messaging.IntegrationTest.ProducerService/Vulthil.Messaging.IntegrationTest.ProducerService.csproj")
    .WithReference(rabbitMq)
    .WaitFor(rabbitMq)
    .WithHttpHealthCheck("/health", endpointName: "http")
    .WithExternalHttpEndpoints();

builder.AddDotnetProject("consumer", "../Vulthil.Messaging.IntegrationTest.ConsumerService/Vulthil.Messaging.IntegrationTest.ConsumerService.csproj")
    .WithReference(rabbitMq)
    .WaitFor(rabbitMq)
    .WithHttpHealthCheck("/health", endpointName: "http")
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();
