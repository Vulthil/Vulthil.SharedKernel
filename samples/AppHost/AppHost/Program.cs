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

var mySql = builder.AddMySql(ServiceNames.MySqlServerServiceName)
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var shipmentsDb = mySql.AddDatabase(ServiceNames.ShipmentsDatabaseServiceName, "shipments");

var cosmos = builder.AddAzureCosmosDB(ServiceNames.CosmosDbServiceName)
    .RunAsEmulator(emulator => emulator
        .WithDataExplorer()
        .WithLifetime(ContainerLifetime.Persistent));

var catalogDb = cosmos.AddCosmosDatabase(ServiceNames.CatalogDatabaseServiceName, "catalog");

var wepApi = builder.AddDotnetProject(ServiceNames.WebApiServiceName, "../../WebApi/WebApi/WebApi.csproj")
        .WithReference(rmq)
        .WithReference(postgresDb)
        .WaitFor(rmq)
        .WaitFor(postgresDb);

var shipments = builder.AddDotnetProject(ServiceNames.ShipmentsServiceName, "../../Shipments/Shipments/Shipments.csproj")
    .WithReference(rmq)
    .WithReference(shipmentsDb)
    .WaitFor(rmq)
    .WaitFor(shipmentsDb);

var catalog = builder.AddDotnetProject(ServiceNames.CatalogServiceName, "../../Catalog/Catalog/Catalog.csproj")
    .WithReference(rmq)
    .WithReference(catalogDb)
    .WaitFor(rmq)
    .WaitFor(catalogDb);

builder.AddDotnetProject(ServiceNames.WebAppServiceName, "../../WebApp/WebApp/WebApp.csproj")
    .WithReference(wepApi)
    .WaitFor(wepApi);

builder.AddScalarApiReference(options =>
    {
        options.PreferHttpsEndpoint().AllowSelfSignedCertificates();
        options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    })
    .WithApiReference(wepApi)
    .WithApiReference(shipments)
    .WithApiReference(catalog)
    .WaitFor(wepApi)
    .WaitFor(shipments)
    .WaitFor(catalog);

await builder.Build().RunAsync();
