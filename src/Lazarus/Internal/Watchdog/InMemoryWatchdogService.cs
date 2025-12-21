using System.Collections.Immutable;
using Lazarus.Public.Watchdog;

namespace Lazarus.Internal.Watchdog;

internal class InMemoryWatchdogService<TService>: IWatchdogService<TService>
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _windowPeriod;
    private readonly List<Heartbeat> _recentHeartbeats;
    private readonly Lock _lock = new();

    public InMemoryWatchdogService(TimeProvider timeProvider, TimeSpan windowPeriod)
    {
        _timeProvider = timeProvider;
        _windowPeriod = windowPeriod;
        _recentHeartbeats = [];
    }

    public void RegisterHeartbeat(Heartbeat report)
    {
        lock (_lock)
        {
            _recentHeartbeats.Add(report);

            DateTimeOffset cutOff = _timeProvider.GetUtcNow() - _windowPeriod;
            _recentHeartbeats.RemoveAll(hb => hb.EndTime <= cutOff);

        }

    }

    public Heartbeat? GetLastHeartbeat()
    {
        lock (_lock)
        {
            return _recentHeartbeats.LastOrDefault();
        }
    }

    public IReadOnlyList<Exception> GetExceptionsInWindow()
    {
        lock (_lock)
        {
            DateTimeOffset cutOff = _timeProvider.GetUtcNow() - _windowPeriod;
            return _recentHeartbeats
                .Where(hb => hb.EndTime > cutOff)
                .Where(hb => hb.Exception is not null)
                .Select(hb => hb.Exception!)
                .ToImmutableList();

        }
    }
}
