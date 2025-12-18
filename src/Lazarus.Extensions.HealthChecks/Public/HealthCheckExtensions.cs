using Lazarus.Extensions.HealthChecks.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Lazarus.Extensions.HealthChecks.Public;

/// <summary>
/// Extension methods for integrating Lazarus service health monitoring with ASP.NET Core health checks.
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Adds a health check for a Lazarus service that monitors the service's heartbeat activity.
    /// The health check will report the service as unhealthy if no heartbeat has been received
    /// within the specified timeout period.
    /// </summary>
    /// <typeparam name="TService">
    /// The type of the Lazarus service to monitor. Must match the type used when registering
    /// the service with <c>AddLazarusService&lt;TService&gt;</c>.
    /// </typeparam>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/> to add the health check to.</param>
    /// <param name="timeout">
    /// The maximum duration allowed since the last heartbeat before the service is considered unhealthy.
    /// For example, a timeout of 30 seconds means the service must register a heartbeat at least once
    /// every 30 seconds to be considered healthy.
    /// </param>
    /// <param name="customName">
    /// An optional custom name for the health check. If not provided, a name will be auto-generated
    /// using the service type name and a random identifier.
    /// </param>
    /// <param name="failureStatus">
    /// The <see cref="HealthStatus"/> to report when the health check fails.
    /// Defaults to <see cref="HealthStatus.Unhealthy"/>.
    /// </param>
    /// <param name="tags">
    /// Optional tags to associate with the health check for filtering and categorization.
    /// </param>
    /// <returns>The <see cref="IHealthChecksBuilder"/> for chaining additional health check registrations.</returns>
    /// <example>
    /// <code>
    /// services.AddHealthChecks()
    ///     .AddLazarusHealthCheck&lt;MyBackgroundService&gt;(
    ///         timeout: TimeSpan.FromSeconds(30),
    ///         customName: "my-service-health",
    ///         tags: new[] { "background-services" });
    /// </code>
    /// </example>
    public static IHealthChecksBuilder AddLazarusHealthCheck<TService>(this IHealthChecksBuilder builder, TimeSpan timeout, string? customName = null, HealthStatus failureStatus = HealthStatus.Unhealthy, IEnumerable<string>? tags = null)
    {
        string name = customName ?? $"{typeof(TService).Name} ({GetRandomHash()}) ";

        builder.AddTypeActivatedCheck<LazarusServiceHealthCheck<TService>>(name, failureStatus, tags ?? [], args: timeout);

        return builder;
    }

    /// <summary>
    /// This is not for anything security related, just for getting nice, short, probably unique names.
    /// </summary>
    /// <returns></returns>
    private static string GetRandomHash()
    {
        byte[] bytes = new byte[4];
        Random.Shared.NextBytes(bytes);
        return Convert.ToHexString(bytes);
    }

}
