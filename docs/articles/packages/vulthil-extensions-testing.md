# Vulthil.Extensions.Testing

Use `Vulthil.Extensions.Testing` for shared testing helpers and extension points.

## When to use

- Reusing assertion and setup helpers across test suites
- Reducing repeated test composition boilerplate
- Polling asynchronous conditions during integration tests

## Pattern

- Keep helpers small and composable
- Prefer intent-revealing test extensions
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
