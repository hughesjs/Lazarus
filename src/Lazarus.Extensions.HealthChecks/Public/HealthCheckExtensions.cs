using Lazarus.Extensions.HealthChecks.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Lazarus.Extensions.HealthChecks.Public;

public static class HealthCheckExtensions
{
    public static IHealthChecksBuilder AddLazarusHealthcheck<TService>(this IHealthChecksBuilder builder, TimeSpan timeout, string? customName = null, HealthStatus failureStatus = HealthStatus.Unhealthy, IEnumerable<string>? tags = null)
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
