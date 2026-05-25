namespace Vulthil.Messaging;

/// <summary>
/// Configures the built-in consume filters that <c>AddMessaging</c> registers by default.
/// Each flag toggles a specific filter on or off; setting a flag to <see langword="false"/>
/// causes the filter to pass deliveries straight through without performing its work.
/// </summary>
/// <remarks>
/// Filters remain registered in DI regardless of these flags so that user code can still
/// resolve them (e.g. for unit tests); the flag is checked at invocation time. To disable
/// every default filter, set each flag explicitly.
/// </remarks>
public sealed class ConsumeFilterOptions
{
    /// <summary>
    /// When <see langword="true"/> (the default), <see cref="Filters.LoggingConsumeFilter{TMessage}"/>
    /// emits structured Debug logs at the start and end of every consume, plus a Warning log on
    /// uncaught exceptions, with timing information.
    /// </summary>
    public bool EnableLogging { get; set; } = true;
}
