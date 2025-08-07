using Vulthil.xUnit;

namespace WebApi.Tests;

public abstract class BaseIntegrationTestCase(CustomWebApplicationFactory factory, ITestOutputHelper? testOutputHelper = null) : BaseIntegrationTestCase<Program>(factory, testOutputHelper), IClassFixture<CustomWebApplicationFactory>;

