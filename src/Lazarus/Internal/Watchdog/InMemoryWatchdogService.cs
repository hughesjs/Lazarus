using System.Collections.Concurrent;

namespace Lazarus.Internal.Watchdog;

internal class InMemoryWatchdogService<TKey>: IWatchdogService<TKey> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, DateTimeOffset> _lastHeartbeats;
    private readonly TimeProvider _timeProvider;

    public InMemoryWatchdogService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _lastHeartbeats = new();
    }

    public void RegisterHeartbeat(TKey key)
    {
        DateTimeOffset currentTime = _timeProvider.GetUtcNow();

        // This is needed to ensure that we're resilient to time-skew
        _lastHeartbeats.AddOrUpdate(
            key,
            currentTime,
            (_, existing) => currentTime > existing ? currentTime : existing
        );
    }

    public DateTimeOffset? GetLastHeartbeat(TKey key) =>
        _lastHeartbeats.TryGetValue(key, out DateTimeOffset lastHeartbeat)
            ? lastHeartbeat
            : null;
}
