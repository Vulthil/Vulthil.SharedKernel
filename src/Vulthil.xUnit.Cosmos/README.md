# Vulthil.xUnit.Cosmos

[![NuGet](https://img.shields.io/nuget/v/Vulthil.xUnit.Cosmos)](https://www.nuget.org/packages/Vulthil.xUnit.Cosmos)

Azure Cosmos DB emulator fixture for `Vulthil.xUnit`: starts the Cosmos emulator as a Testcontainer, registers your `DbContext` against it, and — when shared through a `ContainerHost` — gives every test class its own emulator database so classes run in parallel without sharing data.

## Install

`dotnet add package Vulthil.xUnit.Cosmos`

## Usage

```csharp
internal sealed class CosmosTestContainer(IMessageSink messageSink)
    : CosmosTestContainerFixture<MyCosmosDbContext>(messageSink)
{
    public override string ConnectionStringKey => "cosmosdb";
}
```

## Docs

Usage patterns: https://github.com/Vulthil/Vulthil.SharedKernel/tree/main/docs/articles/packages
