namespace Vulthil.Messaging;

/// <summary>
/// Configures the built-in consume filters that <c>AddMessaging</c> registers by default.
/// Each flag toggles a specific filter on or off; a filter whose flag is <see langword="false"/>
/// is never registered in DI, so deliveries do not pass through it at all.
/// </summary>
/// <remarks>
/// The flags are read once, when <c>AddMessaging</c> composes the pipeline (after the configurator
/// action has run) — set them via configuration (<c>Messaging:Options:ConsumeFilters</c>) or
/// <c>ConfigureMessagingOptions</c> inside the configurator. There is no runtime toggle: changing
/// a flag after registration has no effect.
/// </remarks>
public sealed class ConsumeFilterOptions
{
    /// <summary>
    /// When <see langword="true"/> (the default), <see cref="Filters.LoggingConsumeFilter{TMessage}"/>
    /// emits structured Debug logs at the start and end of every consume, plus a Warning log on
    /// uncaught exceptions, with timing information. When <see langword="false"/>, the filter is
    /// not registered at all.
    /// </summary>
    public bool EnableLogging { get; set; } = true;
}
