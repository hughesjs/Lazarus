using Lazarus.Extensions.HealthChecks.Internal;
using Lazarus.Internal.Watchdog;
using Lazarus.Public.Watchdog;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;

namespace Lazarus.Extensions.HealthChecks.Tests.Unit;

public class LazarusServiceHealthCheckTests
{
    private readonly FakeTimeProvider _timeProvider;
    private readonly IWatchdogService _watchdogService;
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    public LazarusServiceHealthCheckTests()
    {
        _timeProvider = new FakeTimeProvider();
        _watchdogService = new InMemoryWatchdogService(_timeProvider);
    }

    [Test]
    public async Task NoHeartbeat_ReturnsUnhealthy()
    {
        // Arrange
        LazarusServiceHealthCheck<TestService> healthCheck = new(_timeout, _watchdogService, _timeProvider);

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Description).Contains("No heartbeat ever received");
    }

    [Test]
    public async Task HeartbeatWithinTimeout_ReturnsHealthy()
    {
        // Arrange
        _watchdogService.RegisterHeartbeat<TestService>();
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        LazarusServiceHealthCheck<TestService> healthCheck = new(_timeout, _watchdogService, _timeProvider);

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Description).Contains("Last heartbeat received in good time");
        await Assert.That(result.Description).Contains("10");
    }

    [Test]
    public async Task HeartbeatExactlyAtTimeout_ReturnsHealthy()
    {
        // Arrange
        _watchdogService.RegisterHeartbeat<TestService>();
        _timeProvider.Advance(TimeSpan.FromSeconds(30)); // Exactly at timeout

        LazarusServiceHealthCheck<TestService> healthCheck = new(_timeout, _watchdogService, _timeProvider);

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Description).Contains("Last heartbeat received in good time");
    }

    [Test]
    public async Task HeartbeatExceedsTimeout_ReturnsUnhealthy()
    {
        // Arrange
        _watchdogService.RegisterHeartbeat<TestService>();
        _timeProvider.Advance(TimeSpan.FromSeconds(35)); // Beyond timeout

        LazarusServiceHealthCheck<TestService> healthCheck = new(_timeout, _watchdogService, _timeProvider);

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Description).Contains("Last heartbeat received too long ago");
        await Assert.That(result.Description).Contains("35");
    }

    [Test]
    public async Task MultipleHeartbeats_UsesLatest()
    {
        // Arrange
        _watchdogService.RegisterHeartbeat<TestService>();
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        _watchdogService.RegisterHeartbeat<TestService>(); // Second heartbeat
        _timeProvider.Advance(TimeSpan.FromSeconds(5));

        LazarusServiceHealthCheck<TestService> healthCheck = new(_timeout, _watchdogService, _timeProvider);

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        // Should show 5 seconds (from latest heartbeat), not 15 seconds
        await Assert.That(result.Description).Contains("5");
        await Assert.That(result.Description).DoesNotContain("15");
    }

    [Test]
    public async Task DifferentServiceTypes_TrackedSeparately()
    {
        // Arrange
        _watchdogService.RegisterHeartbeat<Service1>();
        _timeProvider.Advance(TimeSpan.FromSeconds(5));
        _watchdogService.RegisterHeartbeat<Service2>();
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        LazarusServiceHealthCheck<Service1> healthCheck1 = new(_timeout, _watchdogService, _timeProvider);
        LazarusServiceHealthCheck<Service2> healthCheck2 = new(_timeout, _watchdogService, _timeProvider);

        // Act
        HealthCheckResult result1 = await healthCheck1.CheckHealthAsync(new HealthCheckContext());
        HealthCheckResult result2 = await healthCheck2.CheckHealthAsync(new HealthCheckContext());

        // Assert
        await Assert.That(result1.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result1.Description).Contains("15"); // 5 + 10 seconds passed

        await Assert.That(result2.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result2.Description).Contains("10"); // Only 10 seconds passed
    }

    [Test]
    public async Task Metadata_ContainsExpectedFields()
    {
        // Arrange
        _watchdogService.RegisterHeartbeat<TestService>();
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        LazarusServiceHealthCheck<TestService> healthCheck = new(_timeout, _watchdogService, _timeProvider);

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        await Assert.That(result.Data).IsNotEmpty();
        await Assert.That(result.Data.ContainsKey("lastHeartbeat")).IsTrue();
        await Assert.That(result.Data.ContainsKey("timePassed")).IsTrue();
        await Assert.That(result.Data.ContainsKey("timeout")).IsTrue();
        await Assert.That(result.Data.ContainsKey("service")).IsTrue();

        // Verify service type in metadata
        await Assert.That(result.Data["service"]).IsEqualTo(nameof(TestService));
    }

    [Test]
    public async Task HealthyResult_IncludesTimingInMessage()
    {
        // Arrange
        _watchdogService.RegisterHeartbeat<TestService>();
        _timeProvider.Advance(TimeSpan.FromSeconds(15));

        LazarusServiceHealthCheck<TestService> healthCheck = new(_timeout, _watchdogService, _timeProvider);

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Description).Contains("15");
        await Assert.That(result.Description).Contains("s ago");
    }

    // Test service marker classes
    private class TestService;
    private class Service1;
    private class Service2;
}
