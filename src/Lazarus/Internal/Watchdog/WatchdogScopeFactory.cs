using Lazarus.Public.Watchdog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lazarus.Internal.Watchdog;

internal class WatchdogScopeFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;
    private readonly IWatchdogService _watchdogService;

    public WatchdogScopeFactory(IServiceProvider serviceProvider, TimeProvider timeProvider, IWatchdogService watchdogService)
    {
        _serviceProvider = serviceProvider;
        _timeProvider = timeProvider;
        _watchdogService = watchdogService;
    }

    public WatchdogScope<TService> CreateScope<TService>()
    {
        return new(_serviceProvider.GetRequiredService<ILogger<WatchdogScope<TService>>>(), _timeProvider, _watchdogService);
    }
}
