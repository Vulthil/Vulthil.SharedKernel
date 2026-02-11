using Scalar.Aspire;
using ServiceDefaults;

var builder = DistributedApplication.CreateBuilder(args);

var username = builder.AddParameter("rabbitmq-username", true);
var password = builder.AddParameter("rabbitmq-password", true);
var rmq = builder.AddRabbitMQ(ServiceNames.RabbitMqServiceName, username, password)
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
        options.PreferHttpsEndpoint().AllowSelfSignedCertificates();
        options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    })
    .WithApiReference(wepApi);

await builder.Build().RunAsync();
