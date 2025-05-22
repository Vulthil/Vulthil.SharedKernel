using Vulthil.xUnit;
using WebApi.Tests;


[assembly: AssemblyFixture(typeof(PostgreSqlPool))]
[assembly: AssemblyFixture(typeof(RabbitMqPod))]
[assembly: AssemblyFixture(typeof(TempCosmos))]

namespace WebApi.Tests;

public sealed class CustomWebApplicationFactory(PostgreSqlPool postgreSqlPool, RabbitMqPod rabbitMqPod, TempCosmos tempCosmos) : BaseWebApplicationFactory<Program>(postgreSqlPool, rabbitMqPod, tempCosmos);
