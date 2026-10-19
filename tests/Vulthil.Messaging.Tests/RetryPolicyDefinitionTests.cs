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

    [Fact]
    public void GetDelayWithJitterStaysWithinTheJitterBandAroundTheInterval()
    {
        // Arrange
        Target.Intervals.Add(TimeSpan.FromSeconds(10));
        Target.JitterFactor = 0.5;

        // Act
        var delays = Enumerable.Range(0, 200).Select(_ => Target.GetDelay(0)).ToList();

        // Assert
        delays.ShouldAllBe(delay => delay >= TimeSpan.FromSeconds(5) && delay <= TimeSpan.FromSeconds(15));
        delays.Distinct().Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public void PoliciesBuiltFromEqualSettingsAreEqualAndHaveTheSameHashCode()
    {
        // Arrange
        var policy = BuildPolicy();
        var other = BuildPolicy();

        // Act
        var equal = policy.Equals(other);

        // Assert
        equal.ShouldBeTrue();
        other.GetHashCode().ShouldBe(policy.GetHashCode());
    }

    [Fact]
    public void PoliciesWithTheSameIgnoredExceptionNamesInAnotherOrderAreEqual()
    {
        // Arrange
        Target.IgnoreExceptions.Add(typeof(ArgumentException).AssemblyQualifiedName!);
        Target.IgnoreExceptions.Add(typeof(TimeoutException).AssemblyQualifiedName!);
        var other = new RetryPolicyDefinition();
        other.IgnoreExceptions.Add(typeof(TimeoutException).AssemblyQualifiedName!);
        other.IgnoreExceptions.Add(typeof(ArgumentException).AssemblyQualifiedName!);

        // Act
        var equal = Target.Equals(other);

        // Assert
        equal.ShouldBeTrue();
    }

    [Fact]
    public void PoliciesWithTheSameIntervalsInAnotherOrderAreNotEqual()
    {
        // Arrange
        Target.Intervals.Add(TimeSpan.FromSeconds(1));
        Target.Intervals.Add(TimeSpan.FromSeconds(5));
        var other = new RetryPolicyDefinition();
        other.Intervals.Add(TimeSpan.FromSeconds(5));
        other.Intervals.Add(TimeSpan.FromSeconds(1));

        // Act
        var equal = Target.Equals(other);

        // Assert
        equal.ShouldBeFalse();
    }

    [Theory]
    [InlineData(nameof(RetryPolicyDefinition.MaxRetryCount))]
    [InlineData(nameof(RetryPolicyDefinition.JitterFactor))]
    [InlineData(nameof(RetryPolicyDefinition.InMemory))]
    [InlineData(nameof(RetryPolicyDefinition.Intervals))]
    [InlineData(nameof(RetryPolicyDefinition.IgnoreExceptions))]
    [InlineData(nameof(RetryPolicyDefinition.AddIgnoredType))]
    public void PoliciesThatDifferInOneSettingAreNotEqual(string setting)
    {
        // Arrange
        var policy = BuildPolicy();
        var other = BuildPolicy();
        ChangeSetting(other, setting);

        // Act
        var equal = policy.Equals(other);

        // Assert
        equal.ShouldBeFalse();
    }

    private static RetryPolicyDefinition BuildPolicy()
    {
        var configurator = new RetryPolicyConfigurator();
        configurator.SetIntervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
        configurator.UseJitter(0.2);
        configurator.Ignore<ArgumentException>();
        return configurator.Build();
    }

    private static void ChangeSetting(RetryPolicyDefinition policy, string setting)
    {
        switch (setting)
        {
            case nameof(RetryPolicyDefinition.MaxRetryCount):
                policy.MaxRetryCount++;
                break;
            case nameof(RetryPolicyDefinition.JitterFactor):
                policy.JitterFactor = 0.5;
                break;
            case nameof(RetryPolicyDefinition.InMemory):
                policy.InMemory = true;
                break;
            case nameof(RetryPolicyDefinition.Intervals):
                policy.Intervals.Add(TimeSpan.FromMinutes(1));
                break;
            case nameof(RetryPolicyDefinition.IgnoreExceptions):
                policy.IgnoreExceptions.Add(typeof(TimeoutException).AssemblyQualifiedName!);
                break;
            case nameof(RetryPolicyDefinition.AddIgnoredType):
                policy.AddIgnoredType(typeof(TimeoutException));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(setting), setting, "Unknown retry setting.");
        }
    }
}
