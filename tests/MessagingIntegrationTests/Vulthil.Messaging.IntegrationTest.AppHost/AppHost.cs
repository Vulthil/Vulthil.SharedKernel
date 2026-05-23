var builder = DistributedApplication.CreateBuilder(args);

var rabbitMq = builder.AddRabbitMQ("rabbitmq")
    .WithLifetime(ContainerLifetime.Session);

builder.AddProject<Projects.Vulthil_Messaging_IntegrationTest_ProducerService>("producer")
    .WithReference(rabbitMq)
    .WaitFor(rabbitMq)
    .WithHttpHealthCheck("/health", endpointName: "http")
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.Vulthil_Messaging_IntegrationTest_ConsumerService>("consumer")
    .WithReference(rabbitMq)
    .WaitFor(rabbitMq)
    .WithHttpHealthCheck("/health", endpointName: "http")
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();
