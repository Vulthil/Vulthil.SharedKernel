using Microsoft.Extensions.Hosting;

namespace Vulthil.Extensions.Hosting;

/// <summary>
/// Marks an <see cref="IHostedService"/> whose execution can be stopped and started again cleanly, so infrastructure
/// may pause it for the duration of an operation that must not run concurrently with it — for example a test harness
/// resetting the database between tests, where a live database-polling relay would otherwise contend with the reset.
/// Implement this only on services whose <see cref="IHostedService.StartAsync"/> / <see cref="IHostedService.StopAsync"/>
/// are idempotent across repeated cycles (a <see cref="BackgroundService"/> whose <c>ExecuteAsync</c> re-runs cleanly).
/// </summary>
public interface IRestartableHostedService : IHostedService;
