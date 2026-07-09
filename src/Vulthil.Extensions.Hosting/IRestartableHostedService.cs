using Microsoft.Extensions.Hosting;

namespace Vulthil.Extensions.Hosting;

/// <summary>
/// Marks an <see cref="IHostedService"/> whose execution can be stopped and started again cleanly, so infrastructure
/// may pause it for the duration of an operation that must not run concurrently with it — for example a test harness
/// resetting the database between tests, where a live database-polling relay would otherwise contend with the reset.
/// Implement this only on services whose <see cref="IHostedService.StartAsync"/> / <see cref="IHostedService.StopAsync"/>
/// are idempotent across repeated cycles.
/// </summary>
/// <remarks>
/// Prefer implementing <see cref="IHostedService"/> directly over inheriting <see cref="BackgroundService"/>: the
/// host observes only the execute task created by a <see cref="BackgroundService"/>'s first start, and it treats a
/// canceled execute task as a fault that stops the host whenever the application is not already shutting down. On
/// .NET 10 that task is scheduled with a <c>Task.Run</c> gated on the stopping token, so a stop that races service
/// startup can cancel the task before <c>ExecuteAsync</c> has run at all — no graceful handling inside
/// <c>ExecuteAsync</c> can prevent the resulting host shutdown.
/// </remarks>
public interface IRestartableHostedService : IHostedService;
