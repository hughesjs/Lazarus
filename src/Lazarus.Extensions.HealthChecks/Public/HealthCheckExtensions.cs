using Lazarus.Extensions.HealthChecks.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Lazarus.Extensions.HealthChecks.Public;

/// <summary>
/// Extension methods for integrating Lazarus service health monitoring with ASP.NET Core health checks.
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Adds a Lazarus service health check with configuration bound from an IConfigurationSection.
    /// </summary>
    /// <typeparam name="TService">The service type to monitor for health status.</typeparam>
    /// <param name="builder">The health checks builder to extend.</param>
    /// <param name="configuration">The configuration section containing health check thresholds and settings.</param>
    /// <param name="customName">Optional custom name for this health check. If null, generates a name from the service type.</param>
    /// <param name="tags">Optional tags to categorise this health check.</param>
    /// <returns>The health checks builder for method chaining.</returns>
    public static IHealthChecksBuilder AddLazarusHealthCheck<TService>(this IHealthChecksBuilder builder,
        IConfigurationSection configuration,
        string? customName = null,
        IEnumerable<string>? tags = null)
    {
        string name = customName ?? $"{typeof(TService).Name} ({GetRandomHash()}) ";

        builder.Services.Configure<LazarusHealthCheckConfiguration<TService>>(configuration);

        return builder.AddCheck<LazarusServiceHealthCheck<TService>>(name, HealthStatus.Unhealthy, tags ?? []);
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
