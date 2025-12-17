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
    public async Task AddLazarusHealthcheck_RegistersHealthCheck()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        services.AddSingleton<IWatchdogService, InMemoryWatchdogService>();

        IHealthChecksBuilder builder = services.AddHealthChecks();

        // Act
        builder.AddLazarusHealthcheck<TestService>(TimeSpan.FromSeconds(30), customName: "TestService");

        // Assert
        ServiceProvider provider = services.BuildServiceProvider();
        HealthCheckService healthCheckService = provider.GetRequiredService<HealthCheckService>();

        HealthReport report = await healthCheckService.CheckHealthAsync();

        await Assert.That(report.Entries).IsNotEmpty();
        await Assert.That(report.Entries.ContainsKey("TestService")).IsTrue();
    }

    [Test]
    public async Task DefaultName_ContainsServiceName()
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

        await Assert.That(report.Entries.Keys.Any(k => k.Contains("TestService"))).IsTrue();
    }

    [Test]
    public async Task CustomName_OverridesDefault()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        services.AddSingleton<IWatchdogService, InMemoryWatchdogService>();

        IHealthChecksBuilder builder = services.AddHealthChecks();

        // Act
        builder.AddLazarusHealthcheck<TestService>(
            TimeSpan.FromSeconds(30),
            customName: "My Custom Health Check"
        );

        // Assert
        ServiceProvider provider = services.BuildServiceProvider();
        HealthCheckService healthCheckService = provider.GetRequiredService<HealthCheckService>();

        HealthReport report = await healthCheckService.CheckHealthAsync();

        await Assert.That(report.Entries.ContainsKey("My Custom Health Check")).IsTrue();
        await Assert.That(report.Entries).HasCount().EqualTo(1);
    }

    [Test]
    public async Task Tags_AreApplied()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        services.AddSingleton<IWatchdogService, InMemoryWatchdogService>();

        IHealthChecksBuilder builder = services.AddHealthChecks();

        // Act
        builder.AddLazarusHealthcheck<TestService>(
            TimeSpan.FromSeconds(30),
            customName: "TestService",
            tags: new[] { "lazarus", "background-service" }
        );

        // Assert
        ServiceProvider provider = services.BuildServiceProvider();
        HealthCheckService healthCheckService = provider.GetRequiredService<HealthCheckService>();

        HealthReport report = await healthCheckService.CheckHealthAsync();

        HealthReportEntry entry = report.Entries["TestService"];
        await Assert.That(entry.Tags).Contains("lazarus");
        await Assert.That(entry.Tags).Contains("background-service");
    }

    [Test]
    public async Task TimeoutParameter_PassedToHealthCheck()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddLogging();
        FakeTimeProvider timeProvider = new();
        services.AddSingleton<TimeProvider>(timeProvider);

        InMemoryWatchdogService watchdog = new(timeProvider);
        services.AddSingleton<IWatchdogService>(watchdog);

        IHealthChecksBuilder builder = services.AddHealthChecks();

        // Register heartbeat and advance time beyond timeout
        watchdog.RegisterHeartbeat<TestService>();
        timeProvider.Advance(TimeSpan.FromSeconds(35));

        // Act
        builder.AddLazarusHealthcheck<TestService>(
            TimeSpan.FromSeconds(30), // 30 second timeout
            customName: "TestService"
        );

        // Assert
        ServiceProvider provider = services.BuildServiceProvider();
        HealthCheckService healthCheckService = provider.GetRequiredService<HealthCheckService>();

        HealthReport report = await healthCheckService.CheckHealthAsync();

        HealthReportEntry entry = report.Entries["TestService"];

        // Should be unhealthy because time passed (35s) > timeout (30s)
        await Assert.That(entry.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(entry.Description).Contains("too long ago");
    }

    // Test service marker class
    private class TestService;
}
