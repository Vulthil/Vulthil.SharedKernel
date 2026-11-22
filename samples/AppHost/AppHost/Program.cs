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

var ordersDb = postgres.AddDatabase(ServiceNames.OrdersDatabaseServiceName, "orders");

var mySql = builder.AddMySql(ServiceNames.MySqlServerServiceName)
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var shipmentsDb = mySql.AddDatabase(ServiceNames.ShipmentsDatabaseServiceName, "shipments");

var cosmos = builder.AddAzureCosmosDB(ServiceNames.CosmosDbServiceName)
    .RunAsEmulator(emulator => emulator
        .WithDataExplorer()
        .WithLifetime(ContainerLifetime.Persistent));

var catalogDb = cosmos.AddCosmosDatabase(ServiceNames.CatalogDatabaseServiceName, "catalog");

var orders = builder.AddDotnetProject(ServiceNames.OrdersServiceName, "../../Orders/Orders/Orders.csproj")
    .WithReference(rmq)
    .WithReference(ordersDb)
    .WaitFor(rmq)
    .WaitFor(ordersDb);

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

builder.AddDotnetProject(ServiceNames.StorefrontServiceName, "../../Storefront/Storefront/Storefront.csproj")
    .WithReference(orders)
    .WithReference(shipments)
    .WithReference(catalog)
    .WaitFor(orders)
    .WaitFor(shipments)
    .WaitFor(catalog);

builder.AddScalarApiReference(options =>
    {
        options.PreferHttpsEndpoint().AllowSelfSignedCertificates();
        options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    })
    .WithApiReference(orders)
    .WithApiReference(shipments)
    .WithApiReference(catalog)
    .WaitFor(orders)
    .WaitFor(shipments)
    .WaitFor(catalog);

await builder.Build().RunAsync();
