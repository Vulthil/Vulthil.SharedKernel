using Vulthil.Results;

namespace Vulthil.Extensions.Testing;

public static class Polling
{
    private static readonly Error Timeout =
        Error.Failure("Polling.Timeout", "The poll timed out.");

    public static async Task<Result<T>> WaitAsync<T>(
        TimeSpan timeout,
        Func<Task<Result<T>>> func,
        TimeSpan? timerTick = null)
    {
        timerTick ??= TimeSpan.FromSeconds(1);
        using var timer = new PeriodicTimer(timerTick.Value);

        DateTime endTimeUtc = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < endTimeUtc &&
               await timer.WaitForNextTickAsync())
        {
            Result<T> result = await func();

            if (result.IsSuccess)
            {
                return result;
            }
        }

        return Result.Failure<T>(Timeout);
    }
}
