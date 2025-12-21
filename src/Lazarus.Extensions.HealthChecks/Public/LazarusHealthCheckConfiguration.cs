namespace Lazarus.Extensions.HealthChecks.Public;

/// <summary>
/// Configuration settings for monitoring the health status of a Lazarus-managed service.
/// Defines thresholds for determining when a service is healthy, degraded, or unhealthy based on heartbeat timing and exception counts.
/// </summary>
/// <typeparam name="TService">The service type being monitored. Used as a discriminator key for service-specific configuration.</typeparam>
public record LazarusHealthCheckConfiguration<TService>
{
    /// <summary>
    /// Gets or sets the maximum time since the last heartbeat before the service is considered unhealthy.
    /// </summary>
    public required TimeSpan UnhealthyTimeSinceLastHeartbeat { get; init; }

    /// <summary>
    /// Gets or sets the sliding time window for counting exceptions.
    /// Only exceptions within this window are counted towards degraded/unhealthy thresholds.
    /// </summary>
    public required TimeSpan ExceptionCounterSlidingWindow { get; init; }

    /// <summary>
    /// Gets or sets the maximum time since the last heartbeat before the service is considered degraded.
    /// Should be less than <see cref="UnhealthyTimeSinceLastHeartbeat"/>.
    /// </summary>
    public required TimeSpan DegradedTimeSinceLastHeartbeat { get; init; }

    /// <summary>
    /// Gets or sets the number of exceptions within the sliding window that triggers a degraded status.
    /// Should be less than <see cref="UnhealthyExceptionCountThreshold"/>.
    /// </summary>
    public required uint DegradedExceptionCountThreshold { get; init; }

    /// <summary>
    /// Gets or sets the number of exceptions within the sliding window that triggers an unhealthy status.
    /// </summary>
    public required uint UnhealthyExceptionCountThreshold { get; init; }
}
