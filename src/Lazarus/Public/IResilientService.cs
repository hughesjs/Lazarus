namespace Lazarus.Public;

/// <summary>
/// Defines a resilient service that can automatically recover from failures.
/// Implement this interface to create a background service that will be managed by Lazarus,
/// with automatic exception isolation and resurrection capabilities.
/// </summary>
/// <remarks>
/// Services implementing this interface will have their <see cref="PerformLoop"/> method called
/// repeatedly in a loop. If an exception occurs, it will be caught and logged, allowing the
/// service to continue operating. The service can be disposed asynchronously when shutdown is required.
/// </remarks>
/// <example>
/// <code>
/// public class MyBackgroundService : IResilientService
/// {
///     public string Name => "MyService";
///
///     public async Task PerformLoop(CancellationToken cancellationToken)
///     {
///         // Do work here - exceptions are automatically handled
///         await ProcessDataAsync(cancellationToken);
///     }
///
///     public ValueTask DisposeAsync() => ValueTask.CompletedTask;
/// }
/// </code>
/// </example>
public interface IResilientService: IAsyncDisposable
{
    /// <summary>
    /// Executes a single iteration of the service's main work loop.
    /// This method is called repeatedly by Lazarus with automatic exception handling.
    /// </summary>
    /// <param name="cancellationToken">
    /// A cancellation token that signals when the service should stop processing.
    /// Check this token regularly to ensure graceful shutdown.
    /// </param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Any exceptions thrown from this method will be caught, logged, and the loop will continue.
    /// This provides resilience against transient failures without crashing the entire service.
    /// </remarks>
    public Task PerformLoop(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the unique name identifier for this service.
    /// Used for logging and health check identification.
    /// </summary>
    public string Name { get; }
}
