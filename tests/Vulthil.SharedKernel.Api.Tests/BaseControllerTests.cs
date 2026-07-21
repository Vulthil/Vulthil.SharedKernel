using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Api.Tests;

public sealed class BaseControllerTests : BaseUnitTestCase<BaseControllerTests.TestController>
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly ILogger _logger = Mock.Of<ILogger>();
    private string? _capturedCategoryName;

    protected override TestController CreateInstance()
    {
        _loggerFactoryMock
            .Setup(factory => factory.CreateLogger(It.IsAny<string>()))
            .Callback<string>(categoryName => _capturedCategoryName = categoryName)
            .Returns(_logger);

        var services = new ServiceCollection();
        services.AddSingleton(_loggerFactoryMock.Object);
        var provider = services.BuildServiceProvider();

        return new TestController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = provider }
            }
        };
    }

    [Fact]
    public void LoggerResolvesCategoryForTheConcreteControllerTypeAndCachesTheInstance()
    {
        // Arrange

        // Act
        var first = Target.ExposeLogger();
        var second = Target.ExposeLogger();

        // Assert
        Assert.Same(_logger, first);
        Assert.Same(first, second);
        Assert.Contains(nameof(TestController), _capturedCategoryName, StringComparison.Ordinal);
        _loggerFactoryMock.Verify(factory => factory.CreateLogger(It.IsAny<string>()), Times.Once);
    }

    public sealed class TestController : BaseController
    {
        public ILogger ExposeLogger() => Logger;
    }
}
