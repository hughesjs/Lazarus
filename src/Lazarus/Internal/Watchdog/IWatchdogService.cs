namespace Lazarus.Internal.Watchdog;

internal interface IWatchdogService<TKey>
{
    public void RegisterHeartbeat(TKey key);
    public DateTimeOffset? GetLastHeartbeat(TKey key);
}
