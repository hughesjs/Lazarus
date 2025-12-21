namespace Lazarus.Public.Watchdog;

/// <summary>
/// Provides heartbeat monitoring capabilities for resilient services.
/// Used to track service liveness and detect unresponsive services.
/// </summary>
/// <typeparam name="TService">
/// The type of the service registering the heartbeat. This is used as the key to identify the service.
/// </typeparam>
/// <remarks>
/// The watchdog service maintains timestamps of when each service last registered a heartbeat.
/// This information can be used by health checks to determine if a service has become
/// unresponsive (e.g., stuck in a long-running operation or deadlock).
/// </remarks>
public interface IWatchdogService<TService>
{
    /// <summary>
    /// Records a heartbeat for the specified service type, indicating that the service is alive and processing.
    /// </summary>
    /// <remarks>
    /// Lazarus automatically calls this method before each loop iteration. You can also call it
    /// manually within your service implementation during long-running operations to indicate continued liveness.
    /// </remarks>
    public void RegisterHeartbeat(Heartbeat report);

    /// <summary>
    /// Gets the timestamp of the last heartbeat registered by the specified service type.
    /// </summary>
    /// <returns>
    /// The <see cref="DateTimeOffset"/> of the last heartbeat, or <c>null</c> if no heartbeat
    /// has ever been registered for this service type.
    /// </returns>
    public Heartbeat? GetLastHeartbeat();

    /// <summary>
    /// Gets the list of exceptions that have occured within the configured sliding window.
    /// </summary>
    /// <returns>The <see cref="List&lt;Exception&gt;"/> containing all of the exceptions thrown within the window.</returns>
    public List<Exception> GetExceptionsInWindow();

}
