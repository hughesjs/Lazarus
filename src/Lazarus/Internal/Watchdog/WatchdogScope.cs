using Lazarus.Public.Watchdog;
using Microsoft.Extensions.Logging;

namespace Lazarus.Internal.Watchdog;

internal class WatchdogScope<TService>: IDisposable
{
    private readonly ILogger<WatchdogScope<TService>> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IWatchdogService _watchdogService;

    private DateTimeOffset? _startTime;
    private Exception? _exception;
    private bool _hasRan;
    private bool _disposed;

    public WatchdogScope(ILogger<WatchdogScope<TService>> logger, TimeProvider timeProvider, IWatchdogService watchdogService)
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
            _disposed = true;
            return;
        }

        if (_startTime is null)
        {
            _logger.LogWarning("Disposing WatchdogScope with unexecuted task");
            return;
        }

        DateTimeOffset endTime = _timeProvider.GetUtcNow();
        _logger.LogDebug("Executing action inside WatchdogScope. Starting at {StartTime}", endTime);
        Heartbeat report = new() { StartTime = _startTime.Value, EndTime = endTime, Exception = _exception, };
        _watchdogService.RegisterHeartbeat<TService>(report);
    }
}
