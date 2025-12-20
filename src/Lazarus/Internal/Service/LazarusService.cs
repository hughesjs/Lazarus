using Lazarus.Internal.Watchdog;
using Lazarus.Public;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lazarus.Internal.Service;

internal class LazarusService<TInnerService> : BackgroundService, IAsyncDisposable where TInnerService : class, IResilientService
{
    private readonly TimeSpan _loopDelay;
    private readonly ILogger<LazarusService<TInnerService>> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly WatchdogScopeFactory _watchdogScopeFactory;
    private readonly TInnerService _innerService;


    internal LazarusService(TimeSpan loopDelay, ILogger<LazarusService<TInnerService>> logger, TimeProvider timeProvider, TInnerService innerService,
        WatchdogScopeFactory watchdogScopeFactory)
    {
        _loopDelay = loopDelay;
        _logger = logger;
        _timeProvider = timeProvider;
        _innerService = innerService;
        _watchdogScopeFactory = watchdogScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_loopDelay, _timeProvider, cancellationToken);
                // ReSharper disable once ConvertToUsingDeclaration - I want this to explicitly show what code is covered
                using (WatchdogScope<TInnerService> scope = _watchdogScopeFactory.CreateScope<TInnerService>())
                {
                    _logger.LogDebug("Performing iteration in lazarus service ({Name})", _innerService.Name);
                    await scope.ExecuteAsync( () =>  _innerService.PerformLoop(cancellationToken));
                }
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

    public async ValueTask DisposeAsync() => await _innerService.DisposeAsync();
}
