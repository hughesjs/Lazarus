using Lazarus.Extensions.HealthChecks.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Lazarus.Extensions.HealthChecks.Public;

public static class HealthCheckExtensions
{
    public static IHealthChecksBuilder AddLazarusHealthcheck<TService>(this IHealthChecksBuilder builder, TimeSpan timeout, string? customName = null, HealthStatus failureStatus = HealthStatus.Unhealthy, IEnumerable<string>? tags = null)
    {
        string name = customName ?? $"{typeof(TService).Name} (Lazarus)";

        builder.AddTypeActivatedCheck<LazarusServiceHealthCheck<TService>>(name, failureStatus, tags ?? [], timeout);

        return builder;
    }
}
