namespace Lazarus.Public.Watchdog;

/// <summary>
/// Provides heartbeat monitoring capabilities for resilient services.
/// Used to track service liveness and detect unresponsive services.
/// </summary>
/// <remarks>
/// The watchdog service maintains timestamps of when each service last registered a heartbeat.
/// This information can be used by health checks to determine if a service has become
/// unresponsive (e.g., stuck in a long-running operation or deadlock).
/// </remarks>
public interface IWatchdogService
{
    /// <summary>
    /// Records a heartbeat for the specified service type, indicating that the service is alive and processing.
    /// </summary>
    /// <typeparam name="TService">
    /// The type of the service registering the heartbeat. This is used as the key to identify the service.
    /// </typeparam>
    /// <remarks>
    /// Lazarus automatically calls this method before each loop iteration. You can also call it
    /// manually within your service implementation during long-running operations to indicate continued liveness.
    /// </remarks>
    public void RegisterHeartbeat<TService>();

    /// <summary>
    /// Gets the timestamp of the last heartbeat registered by the specified service type.
    /// </summary>
    /// <typeparam name="TService">
    /// The type of the service to query. This should match the type used when registering heartbeats.
    /// </typeparam>
    /// <returns>
    /// The <see cref="DateTimeOffset"/> of the last heartbeat, or <c>null</c> if no heartbeat
    /// has ever been registered for this service type.
    /// </returns>
    public DateTimeOffset? GetLastHeartbeat<TService>();
}
