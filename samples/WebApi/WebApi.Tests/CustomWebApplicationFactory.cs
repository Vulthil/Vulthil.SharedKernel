using Vulthil.SharedKernel.xUnit;

namespace WebApi.Tests;

public sealed class CustomWebApplicationFactory : BaseWebApplicationFactory<Program>
{
    public CustomWebApplicationFactory(PostgreSqlPool postgreSqlPool) : base(postgreSqlPool)
    {
    }
}
