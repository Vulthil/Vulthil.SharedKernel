# Vulthil.xUnit

[![NuGet](https://img.shields.io/nuget/v/Vulthil.xUnit)](https://www.nuget.org/packages/Vulthil.xUnit)

Reusable xUnit base infrastructure for integration and unit test composition: auto-mocked unit test base classes, a `WebApplicationFactory` with Testcontainers and HTTP-mock support, and an assembly-wide `ContainerHost` that shares containers across parallel test classes through isolated per-class scopes built into the fixture base classes (a database per class for database containers, a virtual host per class for RabbitMQ; Cosmos lives in `Vulthil.xUnit.Cosmos`).

## Install

`dotnet add package Vulthil.xUnit`

## Docs

Usage patterns: https://github.com/Vulthil/Vulthil.SharedKernel/tree/main/docs/articles/packages
