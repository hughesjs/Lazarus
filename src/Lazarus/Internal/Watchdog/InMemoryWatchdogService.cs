using Lazarus.Public.Watchdog;

namespace Lazarus.Internal.Watchdog;

internal class InMemoryWatchdogService<TService>: IWatchdogService<TService>
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _windowPeriod;

    private List<Heartbeat> _recentHeartbeats;

    public InMemoryWatchdogService(TimeProvider timeProvider, TimeSpan windowPeriod)
    {
        _timeProvider = timeProvider;
        _windowPeriod = windowPeriod;
        _recentHeartbeats = [];
    }

    public void RegisterHeartbeat(Heartbeat report)
    {
        lock (_recentHeartbeats)
        {
            _recentHeartbeats.Add(report);

            // Prune old heartbeats to prevent unbounded memory growth
            DateTimeOffset cutOff = _timeProvider.GetUtcNow() - _windowPeriod;
            _recentHeartbeats = _recentHeartbeats.Where(hb => hb.EndTime > cutOff).ToList();
        }
    }

    public Heartbeat? GetLastHeartbeat()
    {
        lock (_recentHeartbeats)
        {
            return _recentHeartbeats.LastOrDefault();
        }
    }

    public IReadOnlyList<Exception> GetExceptionsInWindow()
    {
        lock (_recentHeartbeats)
        {
            DateTimeOffset cutOff = _timeProvider.GetUtcNow() - _windowPeriod;
            List<Heartbeat> heartbeatsInWindow = _recentHeartbeats.Where(hb => hb.EndTime > cutOff).ToList();
            _recentHeartbeats = heartbeatsInWindow;

            return heartbeatsInWindow.Where(hb => hb.Exception is not null).Select(hb => hb.Exception!).ToList().AsReadOnly();
        }
    }
}
