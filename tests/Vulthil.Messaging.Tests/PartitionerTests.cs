using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests;

public sealed class PartitionerTests : BaseUnitTestCase
{
    [Fact]
    public void ConstructorRejectsNonPositivePartitionCount()
    {
        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => new Partitioner(0));
        Should.Throw<ArgumentOutOfRangeException>(() => new Partitioner(-1));
    }

    [Fact]
    public async Task SameKeyRunsSequentiallyAndInSubmissionOrder()
    {
        // Arrange
        var partitioner = new Partitioner(16);
        var order = new ConcurrentQueue<int>();
        var concurrent = 0;

        async Task Work(int index)
        {
            Interlocked.Increment(ref concurrent).ShouldBe(1, "same-key work must never run concurrently");
            order.Enqueue(index);
            await Task.Delay(5, CancellationToken);
            Interlocked.Decrement(ref concurrent);
        }

        // Act — submit 8 items for the same key in order 0..7.
        var tasks = Enumerable.Range(0, 8)
            .Select(i => partitioner.RunSequentialAsync("same-key", () => Work(i)))
            .ToArray();
        await Task.WhenAll(tasks);

        // Assert
        order.ShouldBe(Enumerable.Range(0, 8));
    }

    [Fact]
    public async Task DifferentKeysRunConcurrently()
    {
        // Arrange
        var cancellationToken = CancellationToken;
        var partitioner = new Partitioner(16);
        var (keyA, keyB) = FindKeysOnDifferentLanes(partitioner);

        using var bothStarted = new CountdownEvent(2);
        using var release = new ManualResetEventSlim(false);

        Task Work() => Task.Run(() =>
        {
            bothStarted.Signal();
            release.Wait(TimeSpan.FromSeconds(5), cancellationToken);
        }, cancellationToken);

        // Act
        var a = partitioner.RunSequentialAsync(keyA, Work);
        var b = partitioner.RunSequentialAsync(keyB, Work);
        var bothRanConcurrently = bothStarted.Wait(TimeSpan.FromSeconds(5), cancellationToken);
        release.Set();
        await Task.WhenAll(a, b);

        // Assert
        bothRanConcurrently.ShouldBeTrue("work on different lanes must be able to run at the same time");
    }

    [Fact]
    public async Task FaultedWorkDoesNotBlockSubsequentWorkOnTheSameLane()
    {
        // Arrange
        var partitioner = new Partitioner(4);
        var secondRan = false;

        // Act
        var faulting = partitioner.RunSequentialAsync("key", () => throw new InvalidOperationException("boom"));
        var next = partitioner.RunSequentialAsync("key", () =>
        {
            secondRan = true;
            return Task.CompletedTask;
        });

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(() => faulting);
        await next;
        secondRan.ShouldBeTrue();
    }

    [Fact]
    public void UsePartitionerRegistersThePartitionForTheMessageType()
    {
        // Arrange
        var options = new MessagingOptions();
        var configurator = new MessagingConfigurator(Host.CreateApplicationBuilder(), options);

        // Act
        configurator.UsePartitioner<PartitionTestMessage>(8, context => context.CorrelationId);

        // Assert
        options.GetPartition(typeof(PartitionTestMessage)).ShouldNotBeNull();
    }

    [Fact]
    public void UsePartitionerWithCountDefaultsTheKeyToCorrelationId()
    {
        // Arrange
        var options = new MessagingOptions();
        var configurator = new MessagingConfigurator(Host.CreateApplicationBuilder(), options);
        var context = new Mock<IMessageContext<PartitionTestMessage>>();
        context.SetupGet(c => c.CorrelationId).Returns("corr-1");

        // Act
        configurator.UsePartitioner<PartitionTestMessage>(8);

        // Assert
        var spec = options.GetPartition(typeof(PartitionTestMessage));
        spec.ShouldNotBeNull();
        var selector = spec.KeySelector.ShouldBeAssignableTo<Func<IMessageContext<PartitionTestMessage>, string?>>();
        selector(context.Object).ShouldBe("corr-1");
    }

    [Fact]
    public void UsePartitionerWithSharedPartitionerDefaultsTheKeyToCorrelationId()
    {
        // Arrange
        var options = new MessagingOptions();
        var configurator = new MessagingConfigurator(Host.CreateApplicationBuilder(), options);
        var partitioner = new Partitioner(4);
        var context = new Mock<IMessageContext<PartitionTestMessage>>();
        context.SetupGet(c => c.CorrelationId).Returns("corr-2");

        // Act
        configurator.UsePartitioner<PartitionTestMessage>(partitioner);

        // Assert
        var spec = options.GetPartition(typeof(PartitionTestMessage));
        spec.ShouldNotBeNull();
        spec.Partitioner.ShouldBeSameAs(partitioner);
        var selector = spec.KeySelector.ShouldBeAssignableTo<Func<IMessageContext<PartitionTestMessage>, string?>>();
        selector(context.Object).ShouldBe("corr-2");
    }

    private static (string KeyA, string KeyB) FindKeysOnDifferentLanes(Partitioner partitioner)
    {
        const string first = "key-0";
        var firstLane = partitioner.GetPartition(first);
        for (var i = 1; i < 1000; i++)
        {
            var candidate = $"key-{i}";
            if (partitioner.GetPartition(candidate) != firstLane)
            {
                return (first, candidate);
            }
        }

        throw new InvalidOperationException("Could not find two keys mapping to different lanes.");
    }

    internal sealed record PartitionTestMessage(string Value);
}
