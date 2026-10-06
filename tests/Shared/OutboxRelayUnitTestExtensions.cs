namespace Vulthil.SharedKernel.Outbox.Testing;

/// <summary>
/// Drives one relay unit of a store the way the relay cycle does in sequence — claim a batch, dispatch each claimed
/// message in turn as a step of the unit, record the outcomes — so store tests exercise a store's transactional boundary
/// without the engine. Linked into every test project that tests a store directly.
/// </summary>
internal static class OutboxRelayUnitTestExtensions
{
    /// <summary>
    /// Runs one relay unit and returns the number of messages <paramref name="dispatch"/> delivered.
    /// </summary>
    /// <param name="store">The store under test.</param>
    /// <param name="dispatch">
    /// Delivers one message as a step of the unit; returns <see langword="null"/> on success, or the error to record, or
    /// throws. A returned error or an exception fails the step, so the store undoes it.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <param name="batchSize">The maximum number of messages to claim.</param>
    /// <param name="maxRetries">The retry limit for claiming and dead-lettering.</param>
    public static Task<int> RelayBatchAsync(
        this IOutboxStore store,
        Func<OutboxMessageData, CancellationToken, Task<string?>> dispatch,
        CancellationToken cancellationToken,
        int batchSize = 10,
        int maxRetries = 3)
        => store.RunRelayUnitAsync(
            async (unit, token) =>
            {
                var batch = await unit.ClaimAsync(batchSize, maxRetries, token);
                if (batch.Count == 0)
                {
                    return 0;
                }

                var relayedIds = new List<Guid>();
                var failures = new List<OutboxMessageFailure>();
                foreach (var message in batch)
                {
                    try
                    {
                        await unit.RunStepAsync(
                            async stepToken =>
                            {
                                if (await dispatch(message, stepToken) is { } error)
                                {
                                    throw new InvalidOperationException(error);
                                }
                            },
                            token);
                        relayedIds.Add(message.Id);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException || !token.IsCancellationRequested)
                    {
                        failures.Add(new OutboxMessageFailure(message.Id, exception.Message));
                    }
                }

                await unit.RecordAsync(relayedIds, failures, maxRetries, token);
                return relayedIds.Count;
            },
            cancellationToken);
}
