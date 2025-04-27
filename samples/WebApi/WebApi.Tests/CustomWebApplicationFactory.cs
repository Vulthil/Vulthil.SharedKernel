using Vulthil.SharedKernel.xUnit;
using WebApi.Tests;


[assembly: AssemblyFixture(typeof(PostgreSqlPool))]
[assembly: AssemblyFixture(typeof(RabbitMqPod))]

namespace WebApi.Tests;

public sealed class CustomWebApplicationFactory(PostgreSqlPool postgreSqlPool, RabbitMqPod rabbitMqPod) : BaseWebApplicationFactory<Program>(postgreSqlPool, rabbitMqPod);
