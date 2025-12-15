using System.Collections.Concurrent;

namespace Lazarus.Internal.Watchdog;

internal class WatchdogService<TKey> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, DateTimeOffset> _lastHeartbeats;
    private readonly TimeProvider _timeProvider;

    public WatchdogService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _lastHeartbeats = new();
    }

    public void RegisterHeartbeat(TKey service)
    {
        DateTimeOffset currentTime = _timeProvider.GetUtcNow();
        _lastHeartbeats.AddOrUpdate(
            service,
            currentTime,
            (_, existing) => currentTime > existing ? currentTime : existing
        );
    }

    public DateTimeOffset? GetLastHeartbeat(TKey service) =>
        _lastHeartbeats.TryGetValue(service, out DateTimeOffset lastHeartbeat)
            ? lastHeartbeat
            : null;
}
