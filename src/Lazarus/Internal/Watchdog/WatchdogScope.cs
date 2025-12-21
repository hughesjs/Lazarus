using Lazarus.Public.Watchdog;
using Microsoft.Extensions.Logging;

namespace Lazarus.Internal.Watchdog;

internal class WatchdogScope<TService>: IDisposable
{
    private readonly ILogger<WatchdogScope<TService>> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IWatchdogService<TService> _watchdogService;

    private DateTimeOffset? _startTime;
    private Exception? _exception;
    private bool _hasRan;
    private bool _disposed;

    public WatchdogScope(ILogger<WatchdogScope<TService>> logger, TimeProvider timeProvider, IWatchdogService<TService> watchdogService)
    {
        _logger = logger;
        _timeProvider = timeProvider;
        _watchdogService = watchdogService;
    }

    public async Task ExecuteAsync(Func<Task> action)
    {
        if (_hasRan)
        {
            throw new InvalidOperationException("WatchdogScope has already been run, you cannot re-use this scope");
        }

        try
        {
            _hasRan = true;
            _startTime = _timeProvider.GetUtcNow();
            _logger.LogDebug("Executing action inside WatchdogScope. Starting at {StartTime}", _startTime);
            await action();
        }
        catch (Exception e)
        {
            _exception = e;
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_startTime is null)
        {
            _logger.LogWarning("Disposing WatchdogScope with unexecuted task");
            return;
        }

        DateTimeOffset endTime = _timeProvider.GetUtcNow();
        _logger.LogDebug("Disposing WatchdogScope. Ending at {EndTime}", endTime);
        Heartbeat report = new() { StartTime = _startTime.Value, EndTime = endTime, Exception = _exception };

        try // Need to make sure we don't throw in a disposer or Mads T will cry
        {
            _watchdogService.RegisterHeartbeat(report);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error registering heartbeat for {Service} watchdog", typeof(TService));
        }

    }
}
