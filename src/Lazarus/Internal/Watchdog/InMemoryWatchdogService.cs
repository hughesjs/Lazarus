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

    public void RegisterHeartbeat(Heartbeat report) => _recentHeartbeats.Add(report);

    public Heartbeat? GetLastHeartbeat() => _recentHeartbeats.LastOrDefault();

    public List<Exception> GetExceptionsInWindow()
    {
        DateTimeOffset cutOff = _timeProvider.GetUtcNow() -  _windowPeriod;
        List<Heartbeat> heartbeatsInWindow = _recentHeartbeats.Where(hb => hb.EndTime > cutOff).ToList();
        _recentHeartbeats = heartbeatsInWindow;

        return heartbeatsInWindow.Where(hb => hb.Exception is not null).Select(hb => hb.Exception!).ToList();
    }
}
