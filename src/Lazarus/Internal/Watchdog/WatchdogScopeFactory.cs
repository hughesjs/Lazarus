using Lazarus.Public.Watchdog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lazarus.Internal.Watchdog;

internal class WatchdogScopeFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;

    public WatchdogScopeFactory(IServiceProvider serviceProvider, TimeProvider timeProvider)
    {
        _serviceProvider = serviceProvider;
        _timeProvider = timeProvider;
    }

    public WatchdogScope<TService> CreateScope<TService>()
    {
        return new(_serviceProvider.GetRequiredService<ILogger<WatchdogScope<TService>>>(),
            _timeProvider,
            _serviceProvider.GetRequiredService<IWatchdogService<TService>>());
    }
}
