using Vulthil.Messaging.Queues;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests;

public sealed class RetryPolicyDefinitionTests : BaseUnitTestCase<RetryPolicyDefinition>
{
    protected override RetryPolicyDefinition CreateInstance() => new();

    [Fact]
    public void GetIgnoredExceptionTypesResolvesAssemblyQualifiedConfigNames()
    {
        // Arrange
        Target.IgnoreExceptions.Add(typeof(InvalidOperationException).AssemblyQualifiedName!);

        // Act
        var ignored = Target.GetIgnoredExceptionTypes();

        // Assert
        ignored.ShouldBe([typeof(InvalidOperationException)]);
    }

    [Fact]
    public void GetIgnoredExceptionTypesSkipsUnresolvableNamesButKeepsResolvableOnes()
    {
        // Arrange
        Target.IgnoreExceptions.Add("Does.Not.Exist.Exception, Nowhere");
        Target.IgnoreExceptions.Add(typeof(TimeoutException).AssemblyQualifiedName!);

        // Act
        var ignored = Target.GetIgnoredExceptionTypes();

        // Assert
        ignored.ShouldBe([typeof(TimeoutException)]);
    }

    [Fact]
    public void GetIgnoredExceptionTypesMergesFluentlyIgnoredTypesWithConfigSuppliedNames()
    {
        // Arrange — a fluently built policy (Ignore<T>) whose IgnoreExceptions is extended afterwards, e.g. by config binding.
        var configurator = new RetryPolicyConfigurator();
        configurator.Immediate(2);
        configurator.Ignore<ArgumentException>();
        var policy = configurator.Build();
        policy.IgnoreExceptions.Add(typeof(InvalidOperationException).AssemblyQualifiedName!);

        // Act
        var ignored = policy.GetIgnoredExceptionTypes();

        // Assert
        ignored.ShouldBe([typeof(ArgumentException), typeof(InvalidOperationException)], ignoreOrder: true);
    }

    [Fact]
    public void GetIgnoredExceptionTypesIsFrozenAfterTheFirstAccess()
    {
        // Arrange
        Target.IgnoreExceptions.Add(typeof(InvalidOperationException).AssemblyQualifiedName!);
        var first = Target.GetIgnoredExceptionTypes();

        // Act
        Target.IgnoreExceptions.Add(typeof(TimeoutException).AssemblyQualifiedName!);
        var second = Target.GetIgnoredExceptionTypes();

        // Assert
        second.ShouldBeSameAs(first);
        second.ShouldBe([typeof(InvalidOperationException)]);
    }

    [Fact]
    public void GetDelayReusesTheLastIntervalForAttemptsBeyondTheConfiguredOnes()
    {
        // Arrange
        Target.Intervals.Add(TimeSpan.FromSeconds(1));
        Target.Intervals.Add(TimeSpan.FromSeconds(2));

        // Act & Assert
        Target.GetDelay(0).ShouldBe(TimeSpan.FromSeconds(1));
        Target.GetDelay(1).ShouldBe(TimeSpan.FromSeconds(2));
        Target.GetDelay(5).ShouldBe(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void GetDelayReturnsZeroWhenNoIntervalsAreConfigured()
    {
        // Act & Assert
        Target.GetDelay(0).ShouldBe(TimeSpan.Zero);
    }
}
