using Lazarus.Public.Watchdog;

namespace Lazarus.Internal.Watchdog;

internal class InMemoryWatchdogService<TService>: IWatchdogService<TService>
{
    private Heartbeat? _lastHeartbeat;
    private readonly TimeProvider _timeProvider;

    public InMemoryWatchdogService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;

    }

    public void RegisterHeartbeat(Heartbeat report) =>
        // This is needed to ensure that we're resilient to time-skew
        _lastHeartbeat = report;


    public Heartbeat? GetLastHeartbeat() => _lastHeartbeat;

}
