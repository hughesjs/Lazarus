using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lazarus.Internal;

internal abstract class LazarusService : BackgroundService
{
    private readonly TimeSpan _loopDelay;
    private readonly ILogger<LazarusService> _logger;


    protected LazarusService(TimeSpan loopDelay, ILogger<LazarusService> logger)
    {
        _loopDelay = loopDelay;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                PerformLoop(cancellationToken);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation(ex,  "Cancellation of Lazarus service requested");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception in Lazarus service loop, continuing");
            }
        }

        return Task.CompletedTask;
    }

    protected abstract Task PerformLoop(CancellationToken cancellationToken);
}
