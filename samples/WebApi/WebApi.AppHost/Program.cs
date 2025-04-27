using WebApi.ServiceDefaults;

var builder = DistributedApplication.CreateBuilder(args);

var rmq = builder.AddRabbitMQ(ServiceNames.RabbitMqServiceName)
    .WithManagementPlugin();

var postgres = builder.AddPostgres(ServiceNames.PostgresSqlServerServiceName);

builder.AddProject<Projects.WebApi>(ServiceNames.WebApiServiceName)
        .WithReference(rmq)
        .WithReference(postgres)
        .WaitFor(rmq)
        .WaitFor(postgres);


await builder.Build().RunAsync();
