# Samples

Three services that use the Vulthil packages together. One Aspire AppHost runs them with their databases and one
RabbitMQ broker.

| Sample | Storage | What it shows |
|---|---|---|
| [WebApi](WebApi) | PostgreSQL | Layered service: endpoints and a controller, the CQRS pipeline, transactional outbox and relational inbox, consume and publish filters, a send to Shipments |
| [Shipments](Shipments) | MySQL | Ordered (partitioned) processing, polymorphic consumers, request/reply, faults, configuration-driven topology, a restartable background service, a custom retention sweep |
| [Catalog](Catalog) | Cosmos DB | Cosmos outbox and inbox, and a module assembly ([Catalog.Search](Catalog/Catalog.Search)) registered with the per-assembly entry points |

## Run

Docker must run. Start the AppHost:

```shell
dotnet run --project samples/AppHost/AppHost --launch-profile https
```

The dashboard link is in the output. Scalar shows the API of all three services.

The services talk to each other:

1. `POST /api/main-entities/{id}/shipments` on WebApi sends `CreateShipment` to the `shipment-commands` queue of
   Shipments.
2. Shipments asks the carrier for a delivery estimate (request/reply) and creates the shipment.
3. The carrier simulator in Shipments publishes a scan every 5 seconds until the shipment is delivered.
4. Shipments publishes `ShipmentDelivered`. WebApi completes the side effect of the entity.

The services share no assembly. Each declares its own message records, and the message URN (and, for a published
event, the exchange) is the contract.

## Tests

Each sample has a test project. Run one like any other test project, for example:

```shell
dotnet test --project samples/Shipments/Shipments.Tests
```

## Feature map

### Domain and results

| Feature | Where |
|---|---|
| `AggregateRoot<TId>`, domain events | [MainEntity.cs](WebApi/WebApi.Domain/MainEntities/MainEntity.cs), [Shipment.cs](Shipments/Shipments/Domain/Shipment.cs) |
| `Entity<TId>` child entity | [TrackingEvent.cs](Shipments/Shipments/Domain/TrackingEvent.cs) |
| `DomainException` mapped to a problem response | [ReservedMainEntityNameException.cs](WebApi/WebApi.Domain/MainEntities/ReservedMainEntityNameException.cs), [ShipmentNotFoundException.cs](Shipments/Shipments/Domain/ShipmentNotFoundException.cs) |
| `Error` factories (`Validation`, `NotFound`, `Conflict`) | [MainEntityErrors.cs](WebApi/WebApi.Domain/MainEntities/MainEntityErrors.cs), [ShipmentErrors.cs](Shipments/Shipments/Domain/ShipmentErrors.cs) |
| `Ensure`, `Map`, `Tap`, `Bind` | [MainEntity.cs](WebApi/WebApi.Domain/MainEntities/MainEntity.cs), [Shipment.cs](Shipments/Shipments/Domain/Shipment.cs), [AddProductCommandHandler.cs](Catalog/Catalog/Application/AddProductCommandHandler.cs) |
| `Combine` | [CreateMainEntitiesCommand.cs](WebApi/WebApi.Application/MainEntities/CreateMany/CreateMainEntitiesCommand.cs), [GetAll](WebApi/WebApi.Application/MainEntities/GetAll/GetMainEntityByIdQuery.cs) |
| `Zip`, `OrElse`, `TapError` | [GetById](WebApi/WebApi.Application/MainEntities/GetById/GetMainEntityByIdQuery.cs) |
| `Recover` | [GetAll](WebApi/WebApi.Application/MainEntities/GetAll/GetMainEntityByIdQuery.cs) |
| `Match` | [Ship.cs](WebApi/WebApi/MainEntity/Ship.cs), [CreateShipmentConsumer.cs](Shipments/Shipments/Application/Shipments/CreateShipmentConsumer.cs) |
| `ToResult`, `ToResultAsync` | [GetShipmentQueryHandler.cs](Shipments/Shipments/Application/Shipments/GetShipmentQueryHandler.cs) |

### Application

| Feature | Where |
|---|---|
| `AddApplication` with the logging, domain-event logging, validation and transactional behaviors | [WebApi.Application/DependencyInjection.cs](WebApi/WebApi.Application/DependencyInjection.cs) |
| Per-assembly entry points `AddHandlers` and `AddFluentValidation` | [SearchModule.cs](Catalog/Catalog.Search/SearchModule.cs) |
| Custom domain-event pipeline handler | [DomainEventTimingHandler.cs](Shipments/Shipments/Application/Pipeline/DomainEventTimingHandler.cs), registered in [Program.cs](Shipments/Shipments/Program.cs) |
| `ICommand` (no value), `ICommand<T>`, `IQuery<T>`, `ITransactionalCommand` | [RescheduleDeliveryCommand.cs](Shipments/Shipments/Application/Shipments/RescheduleDeliveryCommand.cs), [AddProductCommand.cs](Catalog/Catalog/Application/AddProductCommand.cs), [ShipMainEntityCommand.cs](WebApi/WebApi.Application/Shipping/ShipMainEntityCommand.cs) |
| FluentValidation with `WithError` | [UpdateMainEntityNameCommandValidator.cs](WebApi/WebApi.Application/MainEntities/Update/UpdateMainEntityNameCommandValidator.cs), [RescheduleDeliveryCommandValidator.cs](Shipments/Shipments/Application/Shipments/RescheduleDeliveryCommandValidator.cs) |
| `ISender`, and handlers injected directly | [GetAll.cs](WebApi/WebApi/MainEntity/GetAll.cs), [RescheduleDelivery.cs](Shipments/Shipments/Endpoints/RescheduleDelivery.cs), [GetProduct.cs](Catalog/Catalog/Endpoints/GetProduct.cs) |
| A consumer that sends a command through the pipeline | [ProductPublishedConsumer.cs](Catalog/Catalog.Search/Indexing/ProductPublishedConsumer.cs) |

### API

| Feature | Where |
|---|---|
| `IEndpoint`, `AddEndpoints`, `MapEndpoints` (also with a route group) | [WebApi/Program.cs](WebApi/WebApi/Program.cs), [SearchModule.cs](Catalog/Catalog.Search/SearchModule.cs) |
| `ToIResult`, `ToCreatedAtRouteHttpResult`, `ProducesErrors` | [Rename.cs](WebApi/WebApi/MainEntity/Rename.cs), [AddProduct.cs](Catalog/Catalog/Endpoints/AddProduct.cs) |
| `CustomResults.Problem` | [Ship.cs](WebApi/WebApi/MainEntity/Ship.cs) |
| `BaseController`, `ToActionResult`, typed results with `[ProducesError]` | [SideEffectsController.cs](WebApi/WebApi/SideEffects/SideEffectsController.cs) |
| `AddProblemDetailsHandling`, `AddOpenApiServices` | any `Program.cs` |

### Persistence and outbox

| Feature | Where |
|---|---|
| `BaseDbContext`, and the same context without it | [WebApiDbContext.cs](WebApi/WebApi.Infrastructure/Data/WebApiDbContext.cs) |
| `UseNpgsql` with settings, `ApplyNpgsqlOutbox` | [WebApi.Infrastructure/DependencyInjection.cs](WebApi/WebApi.Infrastructure/DependencyInjection.cs) |
| `UseMySql` with settings, `ApplyMySqlOutbox`, EF migrations for MySQL | [Shipments/Infrastructure](Shipments/Shipments/Infrastructure) |
| `UseCosmosDb` with settings, `ApplyCosmosOutbox`, `EnsureCreatedAsync` | [Catalog/Infrastructure](Catalog/Catalog/Infrastructure), [Catalog/Program.cs](Catalog/Catalog/Program.cs) |
| `MigrateAsync` | [WebApi/Program.cs](WebApi/WebApi/Program.cs), [Shipments/Program.cs](Shipments/Shipments/Program.cs) |
| Outbox options: batch size, retries, back-off, parallel dispatch, retention with a dead-letter period | [Shipments DI](Shipments/Shipments/Infrastructure/DependencyInjection.cs), [Catalog DI](Catalog/Catalog/Infrastructure/DependencyInjection.cs) |

### Messaging

| Feature | Where |
|---|---|
| `IConsumer`, `IRequestConsumer`, `IRequester` with `SetTimeout` and `RequestErrorCodes` | [CreateShipmentConsumer.cs](Shipments/Shipments/Application/Shipments/CreateShipmentConsumer.cs), [DeliveryEstimateRequestConsumer.cs](Shipments/Shipments/Application/Carrier/DeliveryEstimateRequestConsumer.cs) |
| `IPublisher` with a context (`SetMessageId`, `SetCorrelationId`) | [ProductAddedDomainEventHandler.cs](Catalog/Catalog/Application/ProductAddedDomainEventHandler.cs) |
| Point-to-point send with `ISendEndpointProvider` | [ShipMainEntityCommand.cs](WebApi/WebApi.Application/Shipping/ShipMainEntityCommand.cs) |
| Message URN, exchange, topic exchange, routing keys, correlation ids | [WebApi DI](WebApi/WebApi.Infrastructure/DependencyInjection.cs), [Shipments DI](Shipments/Shipments/Infrastructure/DependencyInjection.cs) |
| Base-type message configuration, polymorphic consumer, `SubscribeAll` | [TrackingUpdate.cs](Shipments/Shipments/Messages/TrackingUpdate.cs), [TrackingUpdateConsumer.cs](Shipments/Shipments/Application/Tracking/TrackingUpdateConsumer.cs) |
| `UsePartitioner` with a shared `Partitioner`, `UseSingleActiveConsumer` | [Shipments DI](Shipments/Shipments/Infrastructure/DependencyInjection.cs) |
| Retry: intervals, immediate, exponential, jitter, in-memory, ignored exceptions; dead-letter queues | [WebApi DI](WebApi/WebApi.Infrastructure/DependencyInjection.cs), [Shipments DI](Shipments/Shipments/Infrastructure/DependencyInjection.cs) |
| Faults (`Fault<T>`) | [TrackingTests.cs](Shipments/Shipments.Tests/TrackingTests.cs) |
| Open and closed consume filters, short-circuiting | [LoggingScopeConsumeFilter.cs](WebApi/WebApi.Infrastructure/Messaging/LoggingScopeConsumeFilter.cs), [ShipmentDeliveredValidationFilter.cs](WebApi/WebApi.Application/Shipping/ShipmentDeliveredValidationFilter.cs) |
| Publish filter | [SourceServiceHeaderPublishFilter.cs](WebApi/WebApi.Infrastructure/Messaging/SourceServiceHeaderPublishFilter.cs) |
| Queues, messages and transport options from configuration | [Shipments appsettings.json](Shipments/Shipments/appsettings.json) |
| `UseRabbitMq` | every DI setup |
| `AddTransactionalOutbox` | [WebApi DI](WebApi/WebApi.Infrastructure/DependencyInjection.cs), [Shipments DI](Shipments/Shipments/Infrastructure/DependencyInjection.cs) |
| `AddTransactionalConsumer` | [WebApi DI](WebApi/WebApi.Infrastructure/DependencyInjection.cs) |
| `AddIdempotentInbox` on the message id, on a key selector, and on an `IInboxKeySelector<T>` | [Shipments DI](Shipments/Shipments/Infrastructure/DependencyInjection.cs), [TrackingUpdateKeySelector.cs](Shipments/Shipments/Application/Tracking/TrackingUpdateKeySelector.cs), [WebApi DI](WebApi/WebApi.Infrastructure/DependencyInjection.cs) |
| Relational inbox on PostgreSQL and MySQL | [WebApiDbContext.cs](WebApi/WebApi.Infrastructure/Data/WebApiDbContext.cs), [ShipmentsDbContext.cs](Shipments/Shipments/Infrastructure/ShipmentsDbContext.cs) |
| Cosmos inbox | [CatalogDbContext.cs](Catalog/Catalog/Infrastructure/CatalogDbContext.cs) |

### Hosting

| Feature | Where |
|---|---|
| `RestartableBackgroundService` | [CarrierSimulator.cs](Shipments/Shipments/Application/Carrier/CarrierSimulator.cs) |
| `AddRetentionSweep` for a store of your own | [ShipmentsModule.cs](Shipments/Shipments/Application/ShipmentsModule.cs), [DeliveredShipmentArchive.cs](Shipments/Shipments/Application/Retention/DeliveredShipmentArchive.cs) |

### Testing

| Feature | Where |
|---|---|
| `BaseUnitTestCase`, `CreateInstance`, `GetMock` | [SourceServiceHeaderPublishFilterTests.cs](WebApi/WebApi.Tests/Unit/SourceServiceHeaderPublishFilterTests.cs) |
| `BaseUnitTestCase<TTarget>`, `Use` | [MainEntityCreatedEventHandlerTests.cs](WebApi/WebApi.Tests/Unit/MainEntityCreatedEventHandlerTests.cs), [DeliveryEstimateRequestConsumerTests.cs](Shipments/Shipments.Tests/DeliveryEstimateRequestConsumerTests.cs) |
| `ContainerHost`, PostgreSQL, MySQL and RabbitMQ containers | [WebApi.Tests/Fixtures](WebApi/WebApi.Tests/Fixtures), [Shipments.Tests/Fixtures](Shipments/Shipments.Tests/Fixtures) |
| `CosmosTestContainerFixture` | [CatalogCosmosContainer.cs](Catalog/Catalog.Tests/Fixtures/CatalogCosmosContainer.cs) |
| `BaseWebApplicationFactory`, `ShouldUseContainer`, `ConfigureCustomWebHost` | [HarnessWebApplicationFactory.cs](Shipments/Shipments.Tests/Fixtures/HarnessWebApplicationFactory.cs) |
| HTTP mocks for typed and named clients | [ExternalWeatherClientTests.cs](WebApi/WebApi.Tests/ExternalWeatherClientTests.cs) |
| A service replaced for one test (`Use<TimeProvider>`) | [SideEffectIntegrationTests.cs](WebApi/WebApi.Tests/SideEffectIntegrationTests.cs) |
| `IResettableTestState` | [InMemoryCustomerNotifier.cs](Shipments/Shipments.Tests/Fixtures/InMemoryCustomerNotifier.cs) |
| Messaging test harness: `ReplaceTransportWithTestHarness`, `Published`, `Sent`, `Consumed`, `Requested`, `Respond`, `Handle` | [ShippingTests.cs](WebApi/WebApi.Tests/ShippingTests.cs), [CreateShipmentTests.cs](Shipments/Shipments.Tests/CreateShipmentTests.cs), [TrackingTests.cs](Shipments/Shipments.Tests/TrackingTests.cs) |
| `Polling.WaitAsync` with `Result`, `bool` and `PollingOptions`; `GetResponseAsync` | [TrackingTests.cs](Shipments/Shipments.Tests/TrackingTests.cs), [CatalogTests.cs](Catalog/Catalog.Tests/CatalogTests.cs) |

## Not in the samples

These are extension points for library and transport authors, not for an application:

- A custom transport on the `Vulthil.Messaging.Transport` SDK. The in-memory test harness
  (`src/Vulthil.Messaging.TestHarness`) is a complete transport built on it.
- A custom `IOutboxStore`, `IOutboxDispatcher` or `IIdempotencyStore`, and a store for another relational provider.
  The provider packages in `src` are the reference implementations.
