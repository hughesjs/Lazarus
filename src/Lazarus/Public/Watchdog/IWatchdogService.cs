namespace Lazarus.Public.Watchdog;

public interface IWatchdogService
{
    public void RegisterHeartbeat<TService>();
    public DateTimeOffset? GetLastHeartbeat<TService>();
}
