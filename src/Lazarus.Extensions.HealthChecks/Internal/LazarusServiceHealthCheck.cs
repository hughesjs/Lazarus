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
        List<Exception> exceptions = _watchdogService.GetExceptionsInWindow();
        StringBuilder statusBuilder = new();
        HealthStatus heartbeatStatus = CheckHeartbeatStatus(statusBuilder, lastHeartbeat);
        HealthStatus exceptionsStatus = CheckExceptionsStatus(statusBuilder, exceptions);

        // This gives us the worst status
        HealthStatus overallStatus = (HealthStatus)int.Min((int)heartbeatStatus, (int)exceptionsStatus);

        return ConstructHealthCheckResult(heartbeatStatus, exceptionsStatus, overallStatus, lastHeartbeat, statusBuilder.ToString(), exceptions.Count);
    }

    private Task<HealthCheckResult> ConstructHealthCheckResult(HealthStatus heartbeatStatus, HealthStatus exceptionsStatus, HealthStatus overallStatus,
        Heartbeat? lastHeartbeat, string status, int exceptionCount)
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
            ["exceptionsInWindow"] = exceptionCount,
        };
        return Task.FromResult(new HealthCheckResult(overallStatus, status, lastHeartbeat?.Exception, metaDict));
    }

    private HealthStatus CheckExceptionsStatus(StringBuilder statusBuilder, List<Exception> exceptions)
    {
        int exceptionCount = exceptions.Count;

        if (exceptionCount == 0)
        {
            statusBuilder.AppendLine("No exceptions found");
            return HealthStatus.Healthy;
        }

        if (exceptionCount < _configuration.CurrentValue.DegradedExceptionCountThreshold)
        {
            statusBuilder.AppendLine($"Some exceptions found ({exceptionCount}), these are likely transient");
            return HealthStatus.Healthy;
        }

        if (exceptionCount < _configuration.CurrentValue.UnhealthyExceptionCountThreshold)
        {
            statusBuilder.AppendLine($"Some exceptions found ({exceptionCount}), service may be experiencing issues");
            return HealthStatus.Degraded;
        }

        statusBuilder.AppendLine($"Many exceptions found ({exceptionCount}), service is experiencing issues");
        return HealthStatus.Unhealthy;
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
