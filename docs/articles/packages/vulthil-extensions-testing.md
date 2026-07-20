# Vulthil.Extensions.Testing

Use `Vulthil.Extensions.Testing` for framework-agnostic test helpers. It has no xUnit dependency — the
xUnit-coupled test stack (base classes, fixtures, containers) lives in [`Vulthil.xUnit`](vulthil-xunit.md).

## When to use

- Polling asynchronous conditions during integration tests (`Polling.WaitAsync`)
- Reading and asserting JSON HTTP responses (`GetResponseAsync<T>`)
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

## HTTP responses

`GetResponseAsync<T>` asserts an `HttpResponseMessage` indicates success and deserializes its JSON body:

```csharp
var response = await client.GetAsync("/weather/london");
var forecast = await response.GetResponseAsync<Forecast>();
```
