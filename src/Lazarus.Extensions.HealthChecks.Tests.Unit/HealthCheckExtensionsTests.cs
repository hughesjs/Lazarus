using Lazarus.Extensions.HealthChecks.Public;
using Lazarus.Internal.Watchdog;
using Lazarus.Public;
using Lazarus.Public.Watchdog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Lazarus.Extensions.HealthChecks.Tests.Unit;

public class HealthCheckExtensionsTests
{
    [Test]
    public async Task AddLazarusHealthcheckRegistersHealthCheck()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        services.AddSingleton<IWatchdogService, InMemoryWatchdogService>();

        IHealthChecksBuilder builder = services.AddHealthChecks();

        // Act
        builder.AddLazarusHealthcheck<TestService>(TimeSpan.FromSeconds(30));

        // Assert
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
        services.AddSingleton<IWatchdogService, InMemoryWatchdogService>();

        IHealthChecksBuilder builder = services.AddHealthChecks();

        builder.AddLazarusHealthcheck<TestService>(TimeSpan.FromSeconds(30));


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
        services.AddSingleton<IWatchdogService, InMemoryWatchdogService>();

        IHealthChecksBuilder builder = services.AddHealthChecks();

        builder.AddLazarusHealthcheck<TestService>(
            TimeSpan.FromSeconds(30),
            customName: "My Custom Health Check"
        );

        ServiceProvider provider = services.BuildServiceProvider();
        HealthCheckService healthCheckService = provider.GetRequiredService<HealthCheckService>();

        HealthReport report = await healthCheckService.CheckHealthAsync();

        await Assert.That(report.Entries.ContainsKey("My Custom Health Check")).IsTrue();
    }

    private class TestService;
}
