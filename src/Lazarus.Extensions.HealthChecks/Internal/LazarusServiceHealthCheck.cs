using System.Text;
using Lazarus.Extensions.HealthChecks.Public;
using Lazarus.Public.Watchdog;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Lazarus.Extensions.HealthChecks.Internal;

internal class LazarusServiceHealthCheck<TService> : IHealthCheck
{
    private readonly TimeProvider _timeProvider;
    private readonly IOptionsMonitor<LazarusHealthCheckConfiguration<TService>> _configuration;
    private readonly IWatchdogService<TService> _watchdogService;

    public LazarusServiceHealthCheck(IWatchdogService<TService> watchdogService, TimeProvider timeProvider,
        IOptionsMonitor<LazarusHealthCheckConfiguration<TService>> configuration)
    {
        _watchdogService = watchdogService;
        _timeProvider = timeProvider;
        _configuration = configuration;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new())
    {
        Heartbeat? lastHeartbeat = _watchdogService.GetLastHeartbeat();
        StringBuilder statusBuilder = new();
        HealthStatus heartbeatStatus = CheckHeartbeatStatus(statusBuilder, lastHeartbeat);
        HealthStatus exceptionsStatus = CheckExceptionsStatus(statusBuilder, lastHeartbeat);

        // This gives us the worst status
        HealthStatus overallStatus = (HealthStatus)int.Min((int)heartbeatStatus, (int)exceptionsStatus);

        return ConstructHealthCheckResult(heartbeatStatus, exceptionsStatus, overallStatus, lastHeartbeat, statusBuilder.ToString());
    }

    private Task<HealthCheckResult> ConstructHealthCheckResult(HealthStatus heartbeatStatus, HealthStatus exceptionsStatus, HealthStatus overallStatus,
        Heartbeat? lastHeartbeat, string status)
    {
        TimeSpan? timePassed = lastHeartbeat is null ? null : _timeProvider.GetUtcNow() - lastHeartbeat.StartTime;

        Dictionary<string, object> metaDict = new()
        {
            ["lastHeartbeat"] = lastHeartbeat,
            ["timePassed"] = timePassed,
            ["configuration"] = _configuration.CurrentValue,
            ["service"] = typeof(TService).Name,
            ["heartbeatStatus"] = heartbeatStatus,
            ["exceptionsStatus"] = exceptionsStatus,
        };
        return Task.FromResult(new HealthCheckResult(overallStatus, status, lastHeartbeat?.Exception, metaDict));
    }

    private HealthStatus CheckExceptionsStatus(StringBuilder statusBuilder, Heartbeat? lastHeartbeat)
    {
        // Temp implementation before sliding window is implemented
        if (lastHeartbeat?.Exception is not null)
        {
            statusBuilder.AppendLine("Exception encountered during last loop");
            return HealthStatus.Degraded;
        }

        statusBuilder.AppendLine("No exceptions encountered in last loop");
        return HealthStatus.Healthy;
    }


    private HealthStatus CheckHeartbeatStatus(StringBuilder statusBuilder, Heartbeat? lastHeartbeat)
    {
        if (lastHeartbeat is null)
        {
            statusBuilder.AppendLine("No heartbeat received, have you registered your service?");
            return HealthStatus.Unhealthy;
        }

        TimeSpan timePassed = _timeProvider.GetUtcNow() - lastHeartbeat.StartTime;

        if (timePassed > _configuration.CurrentValue.UnhealthyTimeSinceLastHeartbeat)
        {
            statusBuilder.AppendLine($"Last heartbeat received too long ago ({timePassed.TotalSeconds}s ago)");
            return HealthStatus.Unhealthy;
        }

        if (timePassed > _configuration.CurrentValue.DegradedTimeSinceLastHeartbeat)
        {
            statusBuilder.AppendLine($"Last heartbeat received too long ago ({timePassed.TotalSeconds}s ago)");
            return HealthStatus.Degraded;
        }

        statusBuilder.AppendLine($"Last heartbeat received in good time ({timePassed.TotalSeconds}s ago)");
        return HealthStatus.Healthy;
    }
}
