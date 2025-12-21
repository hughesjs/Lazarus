using System.Collections.Concurrent;
using Lazarus.Public.Watchdog;

namespace Lazarus.Internal.Watchdog;

internal class InMemoryWatchdogService: IWatchdogService
{
    private readonly ConcurrentDictionary<Type, Heartbeat> _lastHeartbeats;
    private readonly TimeProvider _timeProvider;

    public InMemoryWatchdogService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _lastHeartbeats = new();
    }

    public void RegisterHeartbeat<TService>(Heartbeat report) =>
        // This is needed to ensure that we're resilient to time-skew
        _lastHeartbeats.AddOrUpdate(
            typeof(TService),
            report,
            (_, existing) => report.StartTime > existing.StartTime ? report : existing
        );


    public Heartbeat? GetLastHeartbeat<TService>() => _lastHeartbeats.GetValueOrDefault(typeof(TService));

}
