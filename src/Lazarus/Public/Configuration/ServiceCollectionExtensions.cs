using Lazarus.Internal.Service;
using Lazarus.Internal.Watchdog;
using Lazarus.Public.Watchdog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Lazarus.Public.Configuration;

/// <summary>
/// Extension methods for registering Lazarus resilient services with the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string DUPE_SERVICE_ERROR_MESSAGE = """
                                                   You can't register two Lazarus services of the same type.
                                                   If this is a feature you want, let me know on the Github repo.
                                                   """;

    /// <summary>
    /// Registers a resilient background service with Lazarus, enabling automatic exception handling,
    /// resurrection, and heartbeat monitoring.
    /// </summary>
    /// <typeparam name="TService">
    /// The type of the service to register. Must implement <see cref="IResilientService"/>.
    /// </typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    /// <param name="loopDelay">
    /// The delay between each iteration of the service's <see cref="IResilientService.PerformLoop"/> method.
    /// Use <see cref="TimeSpan.Zero"/> for no delay between iterations.
    /// </param>
    /// <param name="exceptionWindow">
    /// The sliding time window for tracking exceptions. Exceptions older than this window will be discarded
    /// from the watchdog service's exception history. Used by health checks to determine service health based on recent exceptions.
    /// </param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining additional registrations.</returns>
    /// <exception cref="LazarusConfigurationException">
    /// Thrown when attempting to register a service of the same type that is already registered.
    /// </exception>
    /// <example>
    /// <code>
    /// services.AddLazarusService&lt;MyBackgroundService&gt;(
    ///     sp => TimeSpan.FromSeconds(5),
    ///     sp => TimeSpan.FromMinutes(5));
    /// </code>
    /// </example>
    public static IServiceCollection AddLazarusService<TService>(this IServiceCollection services, Func<IServiceProvider, TimeSpan> loopDelay, Func<IServiceProvider, TimeSpan> exceptionWindow) where TService : class, IResilientService
    {
        services.TryAddSingleton(TimeProvider.System);

        // This restriction may prove unnecessary later
        if (services.Any(s => s.ServiceType == typeof(TService)))
        {
            throw new LazarusConfigurationException(DUPE_SERVICE_ERROR_MESSAGE);
        }

        services.AddSingleton<TService>();

        services.TryAddTransient<WatchdogScopeFactory>();
        services.TryAddSingleton<IWatchdogService<TService>>(sp =>
            new InMemoryWatchdogService<TService>(
                sp.GetRequiredService<TimeProvider>(),
                exceptionWindow(sp)));

        services.AddHostedService<LazarusService<TService>>(sp =>
        {
            TService inner = sp.GetRequiredService<TService>();
            ILogger<LazarusService<TService>> logger = sp.GetRequiredService<ILogger<LazarusService<TService>>>();
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            WatchdogScopeFactory watchdog = sp.GetRequiredService<WatchdogScopeFactory>();
            return new(loopDelay(sp), logger, timeProvider, inner, watchdog);
        });
        return services;
    }
}
