namespace Lazarus.Public.Watchdog;

/// <summary>
/// Represents a single execution cycle of a resilient service, capturing timing information and any exceptions that occurred.
/// </summary>
public record Heartbeat
{
    /// <summary>
    /// Gets the time when the service loop execution started.
    /// </summary>
    public DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// Gets the time when the service loop execution completed.
    /// </summary>
    public DateTimeOffset EndTime { get; init; }

    /// <summary>
    /// Gets the exception that occurred during the service loop execution, or null if the execution completed successfully.
    /// </summary>
    public Exception? Exception { get; init; }
}
