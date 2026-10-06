# Vulthil.Extensions.Testing

Use `Vulthil.Extensions.Testing` for framework-agnostic test helpers. It has no xUnit dependency — the
xUnit-coupled test stack (base classes, fixtures, containers) lives in [`Vulthil.xUnit`](vulthil-xunit.md).

## When to use

- Polling asynchronous conditions during integration tests (`Polling.WaitAsync`)
- Reading and asserting JSON HTTP responses (`GetResponseAsync<T>`)
- Resetting the state of a test double between tests (`IResettableTestState`)
- Any test framework — nothing here requires xUnit

## Pattern

- Express the polled condition as a `Result`/`Result<T>` so each failed attempt carries a diagnosable `Error`
- Keep helpers small and composable
- Avoid embedding production logic in test helpers

## Polling

`Polling.WaitAsync` repeatedly invokes a function returning `Result` / `Result<T>` until it succeeds or the timeout elapses.
A linked `CancellationTokenSource` combining the supplied `cancellationToken` with the polling timeout is created internally,
so cancellation flows uniformly through the timer and (via the token-aware overloads) into the function itself.

```csharp
var result = await Polling.WaitAsync(
    TimeSpan.FromSeconds(10),
    async ct =>
    {
        var response = await httpClient.GetAsync("/api/things", ct);
        return response.IsSuccessStatusCode
            ? Result.Success()
            : Result.Failure(Error.Failure("Http", $"Status {response.StatusCode}"));
    },
    TestContext.Current.CancellationToken);

result.IsSuccess.ShouldBeTrue();
```

When polling times out, `PollingResult.PollingError` exposes the individual errors collected from each failed attempt.

### Polling on a fake clock

The timer and the timeout of a poll run on a `TimeProvider`. To drive a poll from a test, pass a fake clock, such as
`FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing`. Pass it in `PollingOptions`, or to the overload
that takes `timerTick` and `timeProvider`. The poll then ticks and times out only when the test advances that clock.
`PollingOptions` takes the timeout; `TimerTick` defaults to one second and `TimeProvider` to `TimeProvider.System`.

```csharp
var clock = new FakeTimeProvider();
var options = new PollingOptions(TimeSpan.FromMinutes(5))
{
    TimerTick = TimeSpan.FromMinutes(1),
    TimeProvider = clock,
};

var polling = Polling.WaitAsync(options, ct => ProbeAsync(ct), cancellationToken);
clock.Advance(TimeSpan.FromMinutes(5));

var result = await polling;
```

The first attempt runs at once, each tick starts the next attempt, and the timeout ends the poll. An advance that
crosses several ticks can start fewer attempts than ticks, because `PeriodicTimer` merges a tick that fires before
the poll has taken the previous one. To run exactly one attempt per tick, advance by one tick, wait until that
attempt has run, and then advance again.

### Exceptions as failed attempts

An exception from the polled function ends the poll and reaches the caller. To poll through expected, temporary
failures, such as a refused connection while a service starts, set `PollingOptions.TreatAsFailedAttempt` to a filter.
An exception that the filter accepts counts as a failed attempt: the poll records it as a `PollingExceptionError`,
which keeps the exception and its stack trace, and tries again at the next tick. Cancellation by the poll's own
timeout or by your token never counts as a failed attempt.

A check that is a plain yes or no can return `bool`. Each `false` records `Polling.ConditionNotMet`.

```csharp
var options = new PollingOptions(TimeSpan.FromSeconds(30))
{
    TimerTick = TimeSpan.FromMilliseconds(500),
    TreatAsFailedAttempt = exception => exception is HttpRequestException,
};

var result = await Polling.WaitAsync(
    options,
    async ct =>
    {
        using var response = await httpClient.GetAsync("/health", ct);
        return response.IsSuccessStatusCode;
    },
    TestContext.Current.CancellationToken);
```

## HTTP responses

`GetResponseAsync<T>` asserts an `HttpResponseMessage` indicates success and deserializes its JSON body:

```csharp
var response = await client.GetAsync("/weather/london");
var forecast = await response.GetResponseAsync<Forecast>();
```

## Resettable test state

`IResettableTestState` marks state that a test double keeps in a test host's services — captured messages, stubs,
an in-memory store. Register the double as an `IResettableTestState`, and a test framework adapter resets it after
each test. `Vulthil.xUnit` resets every instance of every live test host, and the messaging test harness registers
itself as one.

```csharp
services.AddSingleton<InMemoryEmailSender>();
services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<InMemoryEmailSender>());
services.AddSingleton<IResettableTestState>(sp => sp.GetRequiredService<InMemoryEmailSender>());
```

With another test framework, reset the instances from the per-test teardown:

```csharp
foreach (var testState in host.Services.GetServices<IResettableTestState>())
{
    await testState.ResetAsync(cancellationToken);
}
```

See [Resetting your own test doubles](../testing.md#resetting-your-own-test-doubles) for the `Vulthil.xUnit` details.
