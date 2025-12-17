using System.Collections.Concurrent;
using Lazarus.Public.Watchdog;

namespace Lazarus.Internal.Watchdog;

internal class InMemoryWatchdogService: IWatchdogService
{
    private readonly ConcurrentDictionary<Type, DateTimeOffset> _lastHeartbeats;
    private readonly TimeProvider _timeProvider;

    public InMemoryWatchdogService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _lastHeartbeats = new();
    }

    public void RegisterHeartbeat<TService>()
    {
        DateTimeOffset currentTime = _timeProvider.GetUtcNow();

        // This is needed to ensure that we're resilient to time-skew
        _lastHeartbeats.AddOrUpdate(
            typeof(TService),
            currentTime,
            (_, existing) => currentTime > existing ? currentTime : existing
        );
    }

    public DateTimeOffset? GetLastHeartbeat<TService>() =>
        _lastHeartbeats.TryGetValue(typeof(TService), out DateTimeOffset lastHeartbeat)
            ? lastHeartbeat
            : null;
}
