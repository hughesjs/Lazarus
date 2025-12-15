using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lazarus.Internal.Service;

internal abstract class LazarusService : BackgroundService
{
    private readonly TimeSpan _loopDelay;
    private readonly ILogger<LazarusService> _logger;
    private readonly TimeProvider _timeProvider;


    protected LazarusService(TimeSpan loopDelay, ILogger<LazarusService> logger, TimeProvider timeProvider)
    {
        _loopDelay = loopDelay;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Performing iteration in lazarus service ({Name})", CustomName);
                await PerformLoop(cancellationToken);
                await Task.Delay(_loopDelay, _timeProvider, cancellationToken);
            }
            catch (OperationCanceledException e) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation(e, "Cancellation of Lazarus service requested");
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
