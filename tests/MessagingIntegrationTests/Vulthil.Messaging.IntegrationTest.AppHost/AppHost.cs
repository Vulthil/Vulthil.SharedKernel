var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Vulthil_Messaging_IntegrationTest_ProducerService>("producer")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();
