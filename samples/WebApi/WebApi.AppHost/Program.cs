using Scalar.Aspire;
using WebApi.ServiceDefaults;

var builder = DistributedApplication.CreateBuilder(args);

var rmq = builder.AddRabbitMQ(ServiceNames.RabbitMqServiceName)
    .WithManagementPlugin();

var postgres = builder.AddPostgres(ServiceNames.PostgresSqlServerServiceName);

var wepApi = builder.AddProject<Projects.WebApi>(ServiceNames.WebApiServiceName)
        .WithReference(rmq)
        .WithReference(postgres)
        .WaitFor(rmq)
        .WaitFor(postgres);

builder.AddProject<Projects.WebApp>(ServiceNames.WebAppServiceName)
    .WithReference(wepApi)
    .WaitFor(wepApi);


var scalar = builder.AddScalarApiReference(options =>
    {
        options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    })
    .WithApiReference(wepApi);

await builder.Build().RunAsync();
