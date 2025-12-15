using Lazarus.Internal.Service;
using Lazarus.Internal.Watchdog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Lazarus.Public.Configuration;

public static class ServiceCollectionExtensions
{
    private const string DUPE_SERVICE_ERROR_MESSAGE = """
                                                   You can't register two Lazarus services of the same type.
                                                   If this is a feature you want, let me know on the Github repo.
                                                   """;

    public static IServiceCollection AddLazarusService<TService>(this IServiceCollection services, TimeSpan loopDelay) where TService : class, IResilientService
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IWatchdogService<IResilientService>, InMemoryWatchdogService<IResilientService>>();

        // This restriction may prove unnecessary later
        if (services.Any(s => s.ServiceType == typeof(TService)))
        {
            throw new LazarusConfigurationException(DUPE_SERVICE_ERROR_MESSAGE);
        }

        services.AddSingleton<TService>();
        services.AddHostedService<LazarusService<TService>>(sp =>
        {
            TService inner = sp.GetRequiredService<TService>();
            ILogger<LazarusService<TService>> logger = sp.GetRequiredService<ILogger<LazarusService<TService>>>();
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            IWatchdogService<IResilientService> watchdog = sp.GetRequiredService<IWatchdogService<IResilientService>>();
            return new(loopDelay, logger, timeProvider, watchdog, inner);
        });
        return services;
    }
}
