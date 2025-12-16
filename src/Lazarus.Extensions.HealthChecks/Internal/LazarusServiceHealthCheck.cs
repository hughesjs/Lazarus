using Lazarus.Public.Watchdog;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Lazarus.Extensions.HealthChecks.Internal;

internal class LazarusServiceHealthCheck<TService>: IHealthCheck
{
    private readonly TimeSpan _timeout;
    private readonly IWatchdogService _watchdogService;

    public LazarusServiceHealthCheck(TimeSpan timeout, IWatchdogService watchdogService)
    {
        _timeout = timeout;
        _watchdogService = watchdogService;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new())
    {
        DateTimeOffset? lastHeartbeat = _watchdogService.GetLastHeartbeat<TService>();

        throw new NotImplementedException();
    }
}
