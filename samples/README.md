# Samples

A small shop built on the Vulthil packages: three services and a front end. One Aspire AppHost runs them with their
databases and one RabbitMQ broker.

| Sample | Storage | What it shows |
|---|---|---|
| [Orders](Orders) | PostgreSQL | Layered service: endpoints and a controller, the CQRS pipeline, transactional outbox and relational inbox, consume and publish filters, HTTP clients for external services, a send to Shipments |
| [Shipments](Shipments) | MySQL | Ordered (partitioned) processing, polymorphic consumers, request/reply, faults, configuration-driven topology, a restartable background service, a custom retention sweep |
| [Catalog](Catalog) | Cosmos DB | Cosmos outbox and inbox, and a module assembly ([Catalog.Search](Catalog/Catalog.Search)) registered with the per-assembly entry points |
| [Storefront](Storefront) | none | Blazor front end that calls the three services and reads every answer into a `Result` |

## Run

Docker must run. Start the AppHost:

```shell
dotnet run --project samples/AppHost/AppHost --launch-profile https
```

The dashboard link is in the output. Open the Storefront from the dashboard, or use Scalar for the API of each
service.

The services talk to each other:

1. Place an order, then ship it: `POST /api/orders/{id}/shipments` on Orders sends `CreateShipment` to the
   `shipment-commands` queue of Shipments.
2. Shipments asks the carrier for a delivery estimate (request/reply) and creates the shipment.
3. The carrier simulator in Shipments publishes a scan every 5 seconds until the shipment is delivered.
4. Shipments publishes `ShipmentDelivered`. Orders completes the order's fulfillment.

The services share no assembly. Each declares its own message records, and the message URN (and, for a published
event, the exchange) is the contract.

The shipping-rates and inventory services that Orders calls over HTTP do not exist in the sample; the tests replace
them with HTTP mocks.

## Tests

Each service has a test project. Run one like any other test project, for example:

```shell
dotnet test --project samples/Shipments/Shipments.Tests
```

## Feature map

### Domain and results

| Feature | Where |
|---|---|
| `AggregateRoot<TId>`, domain events | [Order.cs](Orders/Orders.Domain/Orders/Order.cs), [Shipment.cs](Shipments/Shipments/Domain/Shipment.cs) |
| `Entity<TId>` child entity | [TrackingEvent.cs](Shipments/Shipments/Domain/TrackingEvent.cs) |
| `DomainException` mapped to a problem response | [OrderAlreadyShippedException.cs](Orders/Orders.Domain/Orders/OrderAlreadyShippedException.cs), [ShipmentNotFoundException.cs](Shipments/Shipments/Domain/ShipmentNotFoundException.cs) |
| `Error` factories (`Validation`, `NotFound`, `Conflict`) | [OrderErrors.cs](Orders/Orders.Domain/Orders/OrderErrors.cs), [ShipmentErrors.cs](Shipments/Shipments/Domain/ShipmentErrors.cs) |
| `Ensure`, `Map`, `Tap`, `Bind` | [Order.cs](Orders/Orders.Domain/Orders/Order.cs), [Shipment.cs](Shipments/Shipments/Domain/Shipment.cs), [AddProductCommandHandler.cs](Catalog/Catalog/Application/AddProductCommandHandler.cs) |
| `Combine` | [PlaceOrdersCommand.cs](Orders/Orders.Application/Orders/PlaceMany/PlaceOrdersCommand.cs), [GetOrdersQuery.cs](Orders/Orders.Application/Orders/GetAll/GetOrdersQuery.cs) |
| `Zip`, `OrElse`, `TapError` | [GetOrderQuery.cs](Orders/Orders.Application/Orders/GetById/GetOrderQuery.cs) |
| `Recover` | [GetOrdersQuery.cs](Orders/Orders.Application/Orders/GetAll/GetOrdersQuery.cs) |
| `Match` | [ShipOrder.cs](Orders/Orders/Endpoints/ShipOrder.cs), [CreateShipmentConsumer.cs](Shipments/Shipments/Application/Shipments/CreateShipmentConsumer.cs) |
| `Result` on the client side: problem responses read into `Error`, `Zip`, `Match`, `TapError` | [ApiResponse.cs](Storefront/Storefront/Api/ApiResponse.cs), [OrderDetails.razor](Storefront/Storefront/Components/Pages/OrderDetails.razor), [Orders.razor](Storefront/Storefront/Components/Pages/Orders.razor) |
| `ToResult`, `ToResultAsync` | [GetShipmentQueryHandler.cs](Shipments/Shipments/Application/Shipments/GetShipmentQueryHandler.cs) |

### Application

| Feature | Where |
|---|---|
| `AddApplication` with the logging, domain-event logging, validation and transactional behaviors | [Orders.Application/DependencyInjection.cs](Orders/Orders.Application/DependencyInjection.cs) |
| Per-assembly entry points `AddHandlers` and `AddFluentValidation` | [SearchModule.cs](Catalog/Catalog.Search/SearchModule.cs) |
| Custom domain-event pipeline handler | [DomainEventTimingHandler.cs](Shipments/Shipments/Application/Pipeline/DomainEventTimingHandler.cs), registered in [Program.cs](Shipments/Shipments/Program.cs) |
| `ICommand` (no value), `ICommand<T>`, `IQuery<T>`, `ITransactionalCommand` | [RescheduleDeliveryCommand.cs](Shipments/Shipments/Application/Shipments/RescheduleDeliveryCommand.cs), [AddProductCommand.cs](Catalog/Catalog/Application/AddProductCommand.cs), [ShipOrderCommand.cs](Orders/Orders.Application/Orders/Ship/ShipOrderCommand.cs) |
| FluentValidation with `WithError` | [ChangeOrderDescriptionCommandValidator.cs](Orders/Orders.Application/Orders/ChangeDescription/ChangeOrderDescriptionCommandValidator.cs), [RescheduleDeliveryCommandValidator.cs](Shipments/Shipments/Application/Shipments/RescheduleDeliveryCommandValidator.cs) |
| `ISender`, and handlers injected directly | [GetOrders.cs](Orders/Orders/Endpoints/GetOrders.cs), [RescheduleDelivery.cs](Shipments/Shipments/Endpoints/RescheduleDelivery.cs), [GetProduct.cs](Catalog/Catalog/Endpoints/GetProduct.cs) |
| A consumer that sends a command through the pipeline | [ProductPublishedConsumer.cs](Catalog/Catalog.Search/Indexing/ProductPublishedConsumer.cs) |

### API

| Feature | Where |
|---|---|
| `IEndpoint`, `AddEndpoints`, `MapEndpoints` (also with a route group) | [Orders/Program.cs](Orders/Orders/Program.cs), [SearchModule.cs](Catalog/Catalog.Search/SearchModule.cs) |
| Failures of external HTTP services turned into results | [GetShippingQuote.cs](Orders/Orders/Endpoints/GetShippingQuote.cs), [GetStock.cs](Orders/Orders/Endpoints/GetStock.cs) |
| `ToIResult`, `ToCreatedAtRouteHttpResult`, `ProducesErrors` | [ChangeOrderDescription.cs](Orders/Orders/Endpoints/ChangeOrderDescription.cs), [AddProduct.cs](Catalog/Catalog/Endpoints/AddProduct.cs) |
| `CustomResults.Problem` | [ShipOrder.cs](Orders/Orders/Endpoints/ShipOrder.cs) |
| `BaseController`, `ToActionResult`, typed results with `[ProducesError]` | [FulfillmentsController.cs](Orders/Orders/Fulfillments/FulfillmentsController.cs) |
| `AddProblemDetailsHandling`, `AddOpenApiServices` | any `Program.cs` |

### Persistence and outbox

| Feature | Where |
|---|---|
| A union type stored in one column and filtered in SQL | [FulfillmentConfiguration.cs](Orders/Orders.Infrastructure/Data/EntityConfigurations/FulfillmentConfiguration.cs), [GetInProgressFulfillmentsQuery.cs](Orders/Orders.Application/Fulfillments/GetInProgress/GetInProgressFulfillmentsQuery.cs) |
| `BaseDbContext`, and the same context without it | [OrdersDbContext.cs](Orders/Orders.Infrastructure/Data/OrdersDbContext.cs) |
| `UseNpgsql` with settings, `ApplyNpgsqlOutbox` | [Orders.Infrastructure/DependencyInjection.cs](Orders/Orders.Infrastructure/DependencyInjection.cs) |
| `UseMySql` with settings, `ApplyMySqlOutbox`, EF migrations for MySQL | [Shipments/Infrastructure](Shipments/Shipments/Infrastructure) |
| `UseCosmosDb` with settings, `ApplyCosmosOutbox`, `EnsureCreatedAsync` | [Catalog/Infrastructure](Catalog/Catalog/Infrastructure), [Catalog/Program.cs](Catalog/Catalog/Program.cs) |
| `MigrateAsync` | [Orders/Program.cs](Orders/Orders/Program.cs), [Shipments/Program.cs](Shipments/Shipments/Program.cs) |
| Outbox options: batch size, retries, back-off, parallel dispatch, retention with a dead-letter period | [Shipments DI](Shipments/Shipments/Infrastructure/DependencyInjection.cs), [Catalog DI](Catalog/Catalog/Infrastructure/DependencyInjection.cs) |

### Messaging

| Feature | Where |
|---|---|
| `IConsumer`, `IRequestConsumer`, `IRequester` with `SetTimeout` and `RequestErrorCodes` | [CreateShipmentConsumer.cs](Shipments/Shipments/Application/Shipments/CreateShipmentConsumer.cs), [DeliveryEstimateRequestConsumer.cs](Shipments/Shipments/Application/Carrier/DeliveryEstimateRequestConsumer.cs) |
| `IPublisher` with a context (`SetMessageId`, `SetCorrelationId`) | [ProductAddedDomainEventHandler.cs](Catalog/Catalog/Application/ProductAddedDomainEventHandler.cs) |
| Point-to-point send with `ISendEndpointProvider` | [ShipOrderCommand.cs](Orders/Orders.Application/Orders/Ship/ShipOrderCommand.cs) |
| Message URN, exchange, topic exchange, routing keys, correlation ids | [Orders DI](Orders/Orders.Infrastructure/DependencyInjection.cs), [Shipments DI](Shipments/Shipments/Infrastructure/DependencyInjection.cs) |
| Base-type message configuration, polymorphic consumer, `SubscribeAll` | [TrackingUpdate.cs](Shipments/Shipments/Messages/TrackingUpdate.cs), [TrackingUpdateConsumer.cs](Shipments/Shipments/Application/Tracking/TrackingUpdateConsumer.cs) |
| `UsePartitioner` with a shared `Partitioner`, `UseSingleActiveConsumer` | [Shipments DI](Shipments/Shipments/Infrastructure/DependencyInjection.cs) |
| Retry: intervals, immediate, exponential, jitter, in-memory, ignored exceptions; dead-letter queues | [Orders DI](Orders/Orders.Infrastructure/DependencyInjection.cs), [Shipments DI](Shipments/Shipments/Infrastructure/DependencyInjection.cs) |
| Faults (`Fault<T>`) | [TrackingTests.cs](Shipments/Shipments.Tests/TrackingTests.cs) |
| Open and closed consume filters, short-circuiting | [LoggingScopeConsumeFilter.cs](Orders/Orders.Infrastructure/Messaging/LoggingScopeConsumeFilter.cs), [ShipmentDeliveredValidationFilter.cs](Orders/Orders.Application/Shipping/ShipmentDeliveredValidationFilter.cs) |
| Publish filter | [SourceServiceHeaderPublishFilter.cs](Orders/Orders.Infrastructure/Messaging/SourceServiceHeaderPublishFilter.cs) |
| Queues, messages and transport options from configuration | [Shipments appsettings.json](Shipments/Shipments/appsettings.json) |
| `UseRabbitMq` | every DI setup |
| `AddTransactionalOutbox` | [Orders DI](Orders/Orders.Infrastructure/DependencyInjection.cs), [Shipments DI](Shipments/Shipments/Infrastructure/DependencyInjection.cs) |
| `AddTransactionalConsumer` | [Orders DI](Orders/Orders.Infrastructure/DependencyInjection.cs) |
| `AddIdempotentInbox` on the message id, on a key selector, and on an `IInboxKeySelector<T>` | [Shipments DI](Shipments/Shipments/Infrastructure/DependencyInjection.cs), [TrackingUpdateKeySelector.cs](Shipments/Shipments/Application/Tracking/TrackingUpdateKeySelector.cs), [Orders DI](Orders/Orders.Infrastructure/DependencyInjection.cs) |
| Relational inbox on PostgreSQL and MySQL | [OrdersDbContext.cs](Orders/Orders.Infrastructure/Data/OrdersDbContext.cs), [ShipmentsDbContext.cs](Shipments/Shipments/Infrastructure/ShipmentsDbContext.cs) |
| Cosmos inbox | [CatalogDbContext.cs](Catalog/Catalog/Infrastructure/CatalogDbContext.cs) |

### Hosting

| Feature | Where |
|---|---|
| `RestartableBackgroundService` | [CarrierSimulator.cs](Shipments/Shipments/Application/Carrier/CarrierSimulator.cs) |
| `AddRetentionSweep` for a store of your own | [ShipmentsModule.cs](Shipments/Shipments/Application/ShipmentsModule.cs), [DeliveredShipmentArchive.cs](Shipments/Shipments/Application/Retention/DeliveredShipmentArchive.cs) |

### Testing

| Feature | Where |
|---|---|
| `BaseUnitTestCase`, `CreateInstance`, `GetMock` | [SourceServiceHeaderPublishFilterTests.cs](Orders/Orders.Tests/Unit/SourceServiceHeaderPublishFilterTests.cs) |
| `BaseUnitTestCase<TTarget>`, `Use` | [OrderPlacedDomainEventHandlerTests.cs](Orders/Orders.Tests/Unit/OrderPlacedDomainEventHandlerTests.cs), [DeliveryEstimateRequestConsumerTests.cs](Shipments/Shipments.Tests/DeliveryEstimateRequestConsumerTests.cs) |
| `ContainerHost`, PostgreSQL, MySQL and RabbitMQ containers | [Orders.Tests/Fixtures](Orders/Orders.Tests/Fixtures), [Shipments.Tests/Fixtures](Shipments/Shipments.Tests/Fixtures) |
| `CosmosTestContainerFixture` | [CatalogCosmosContainer.cs](Catalog/Catalog.Tests/Fixtures/CatalogCosmosContainer.cs) |
| `BaseWebApplicationFactory`, `ShouldUseContainer`, `ConfigureCustomWebHost` | [HarnessWebApplicationFactory.cs](Shipments/Shipments.Tests/Fixtures/HarnessWebApplicationFactory.cs) |
| HTTP mocks for typed and named clients, called through real endpoints | [ExternalServicesTests.cs](Orders/Orders.Tests/ExternalServicesTests.cs) |
| A service replaced for one test (`Use<TimeProvider>`) | [FulfillmentIntegrationTests.cs](Orders/Orders.Tests/FulfillmentIntegrationTests.cs) |
| `IResettableTestState` | [InMemoryCustomerNotifier.cs](Shipments/Shipments.Tests/Fixtures/InMemoryCustomerNotifier.cs) |
| Messaging test harness: `ReplaceTransportWithTestHarness`, `Published`, `Sent`, `Consumed`, `Requested`, `Respond`, `Handle` | [ShippingTests.cs](Orders/Orders.Tests/ShippingTests.cs), [CreateShipmentTests.cs](Shipments/Shipments.Tests/CreateShipmentTests.cs), [TrackingTests.cs](Shipments/Shipments.Tests/TrackingTests.cs) |
| `Polling.WaitAsync` with `Result`, `bool` and `PollingOptions`; `GetResponseAsync` | [TrackingTests.cs](Shipments/Shipments.Tests/TrackingTests.cs), [CatalogTests.cs](Catalog/Catalog.Tests/CatalogTests.cs) |

## Not in the samples

These are extension points for library and transport authors, not for an application:

- A custom transport on the `Vulthil.Messaging.Transport` SDK. The in-memory test harness
  (`src/Vulthil.Messaging.TestHarness`) is a complete transport built on it.
- A custom `IOutboxStore`, `IOutboxDispatcher` or `IIdempotencyStore`, and a store for another relational provider.
  The provider packages in `src` are the reference implementations.
