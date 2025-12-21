using Lazarus.Extensions.HealthChecks.Public;
using Lazarus.Internal.Watchdog;
using Lazarus.Public.Watchdog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;

namespace Lazarus.Extensions.HealthChecks.Tests.Unit;

public class HealthCheckExtensionsTests
{
    [Test]
    public async Task AddLazarusHealthcheckRegistersHealthCheck()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        services.AddSingleton<IWatchdogService<TestService>, InMemoryWatchdogService<TestService>>();

        Dictionary<string, string?> configDict = new()
        {
            ["UnhealthyTimeSinceLastHeartbeat"] = "00:00:30",
            ["DegradedTimeSinceLastHeartbeat"] = "00:00:22.5",
            ["UnhealthyExceptionCountThreshold"] = "5",
            ["DegradedExceptionCountThreshold"] = "2",
            ["ExceptionCounterSlidingWindow"] = "00:05:00"
        };

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        IHealthChecksBuilder builder = services.AddHealthChecks();
        builder.AddLazarusHealthCheck<TestService>(configuration.GetSection(""));

        ServiceProvider provider = services.BuildServiceProvider();
        HealthCheckService healthCheckService = provider.GetRequiredService<HealthCheckService>();

        HealthReport report = await healthCheckService.CheckHealthAsync();

        await Assert.That(report.Entries).IsNotEmpty();
    }

    [Test]
    public async Task DefaultNameUsesServiceName()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        services.AddSingleton<IWatchdogService<TestService>, InMemoryWatchdogService<TestService>>();

        Dictionary<string, string?> configDict = new()
        {
            ["UnhealthyTimeSinceLastHeartbeat"] = "00:00:30",
            ["DegradedTimeSinceLastHeartbeat"] = "00:00:22.5",
            ["UnhealthyExceptionCountThreshold"] = "5",
            ["DegradedExceptionCountThreshold"] = "2",
            ["ExceptionCounterSlidingWindow"] = "00:05:00"
        };

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        IHealthChecksBuilder builder = services.AddHealthChecks();
        builder.AddLazarusHealthCheck<TestService>(configuration.GetSection(""));

        ServiceProvider provider = services.BuildServiceProvider();
        HealthCheckService healthCheckService = provider.GetRequiredService<HealthCheckService>();

        HealthReport report = await healthCheckService.CheckHealthAsync();

        await Assert.That(report.Entries.Single().Key).Contains("TestService");
    }

    [Test]
    public async Task CustomNameOverridesDefault()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        services.AddSingleton<IWatchdogService<TestService>, InMemoryWatchdogService<TestService>>();

        Dictionary<string, string?> configDict = new()
        {
            ["UnhealthyTimeSinceLastHeartbeat"] = "00:00:30",
            ["DegradedTimeSinceLastHeartbeat"] = "00:00:22.5",
            ["UnhealthyExceptionCountThreshold"] = "5",
            ["DegradedExceptionCountThreshold"] = "2",
            ["ExceptionCounterSlidingWindow"] = "00:05:00"
        };

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        IHealthChecksBuilder builder = services.AddHealthChecks();
        builder.AddLazarusHealthCheck<TestService>(
            configuration.GetSection(""),
            customName: "My Custom Health Check"
        );

        ServiceProvider provider = services.BuildServiceProvider();
        HealthCheckService healthCheckService = provider.GetRequiredService<HealthCheckService>();

        HealthReport report = await healthCheckService.CheckHealthAsync();

        await Assert.That(report.Entries.ContainsKey("My Custom Health Check")).IsTrue();
    }

    private class TestService;
}
