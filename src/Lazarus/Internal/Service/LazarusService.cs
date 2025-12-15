using Lazarus.Internal.Watchdog;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lazarus.Internal.Service;

internal abstract class LazarusService : BackgroundService
{
    private readonly TimeSpan _loopDelay;
    private readonly ILogger<LazarusService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IWatchdogService<LazarusService> _watchdogService;


    protected LazarusService(TimeSpan loopDelay, ILogger<LazarusService> logger, TimeProvider timeProvider, IWatchdogService<LazarusService> watchdogService)
    {
        _loopDelay = loopDelay;
        _logger = logger;
        _timeProvider = timeProvider;
        _watchdogService = watchdogService;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Still heartbeat even if we're faulting
            try
            {
                _watchdogService.RegisterHeartbeat(this);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to register heartbeat");
            }

            try
            {
                await Task.Delay(_loopDelay, _timeProvider, cancellationToken);
                _logger.LogDebug("Performing iteration in lazarus service ({Name})", CustomName);
                await PerformLoop(cancellationToken);

            }
            catch (OperationCanceledException e) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation(e, "Cancellation of Lazarus service requested");
                break; // Arguably unnecessary
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception in Lazarus service loop, continuing");
            }
        }
    }

    protected abstract Task PerformLoop(CancellationToken cancellationToken);

    protected virtual string CustomName => "Unnamed";
}
