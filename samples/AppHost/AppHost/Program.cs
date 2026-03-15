using Scalar.Aspire;
using ServiceDefaults;

var builder = DistributedApplication.CreateBuilder(args);

var username = builder.AddParameter("rabbitmqUsername");
var password = builder.AddParameter("rabbitmqPassword", true);
var rmq = builder.AddRabbitMQ(ServiceNames.RabbitMqServiceName, username, password)
    .WithManagementPlugin();

var postgres = builder.AddPostgres("postgres-instance")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var postgresDb = postgres.AddDatabase(ServiceNames.PostgresSqlServerServiceName);

var wepApi = builder.AddProject<Projects.WebApi>(ServiceNames.WebApiServiceName)
        .WithReference(rmq)
        .WithReference(postgresDb)
        .WaitFor(rmq)
        .WaitFor(postgresDb);

builder.AddProject<Projects.WebApp>(ServiceNames.WebAppServiceName)
    .WithReference(wepApi)
    .WaitFor(wepApi);

var scalar = builder.AddScalarApiReference(options =>
    {
        options.PreferHttpsEndpoint().AllowSelfSignedCertificates();
        options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    })
    .WithApiReference(wepApi)
    .WaitFor(wepApi);

await builder.Build().RunAsync();
