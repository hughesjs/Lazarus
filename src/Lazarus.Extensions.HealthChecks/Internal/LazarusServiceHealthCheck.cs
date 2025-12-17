using Lazarus.Public.Watchdog;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Lazarus.Extensions.HealthChecks.Internal;

internal class LazarusServiceHealthCheck<TService>: IHealthCheck
{
    private readonly TimeSpan _timeout;
    private readonly TimeProvider _timeProvider;
    private readonly IWatchdogService _watchdogService;

    public LazarusServiceHealthCheck(TimeSpan timeout, IWatchdogService watchdogService, TimeProvider timeProvider)
    {
        _timeout = timeout;
        _watchdogService = watchdogService;
        _timeProvider = timeProvider;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new())
    {
        DateTimeOffset? lastHeartbeat = _watchdogService.GetLastHeartbeat<TService>();


        if (lastHeartbeat is null)
        {
            return Task.FromResult(new HealthCheckResult(HealthStatus.Unhealthy, "No heartbeat ever received, did you register your service?"));
        }

        TimeSpan timePassed = _timeProvider.GetUtcNow() - lastHeartbeat.Value;
        Dictionary<string, object> metaDict = new() { ["lastHeartbeat"] = lastHeartbeat, ["timePassed"] = timePassed, ["timeout"] = _timeout, ["service"] =  typeof(TService).Name};

        if (timePassed > _timeout)
        {
            return Task.FromResult(new HealthCheckResult(HealthStatus.Unhealthy, $"Last heartbeat received too long ago ({timePassed.TotalSeconds}s ago)", data: metaDict));
        }

        return Task.FromResult(HealthCheckResult.Healthy($"Last heartbeat received in good time ({timePassed.TotalSeconds}s ago)", data: metaDict));
    }
}
