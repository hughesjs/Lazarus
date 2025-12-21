using Lazarus.Extensions.HealthChecks.Internal;
using Lazarus.Extensions.HealthChecks.Public;
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
        _timeProvider = new();
        _watchdogService = new InMemoryWatchdogService(_timeProvider);
    }

    [Test]
    public async Task NoHeartbeatReturnsUnhealthy()
    {
        LazarusServiceHealthCheck<TestService> healthCheck = new(_watchdogService, _timeProvider, CreateOptionsMonitor<TestService>(_timeout));

        HealthCheckResult result = await healthCheck.CheckHealthAsync(new());

        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
            await Assert.That(result.Description).Contains("No heartbeat");
        }
    }

    [Test]
    public async Task HeartbeatWithinTimeoutReturnsHealthy()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        _watchdogService.RegisterHeartbeat<TestService>(new()
        {
            StartTime = now,
            EndTime = now
        });
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        LazarusServiceHealthCheck<TestService> healthCheck = new(_watchdogService, _timeProvider, CreateOptionsMonitor<TestService>(_timeout));

        HealthCheckResult result = await healthCheck.CheckHealthAsync(new());

        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
            await Assert.That(result.Description).Contains("Last heartbeat received in good time");
            await Assert.That(result.Description).Contains("10");
        }
    }


    [Test]
    public async Task HeartbeatExceedsTimeoutReturnsUnhealthy()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        _watchdogService.RegisterHeartbeat<TestService>(new()
        {
            StartTime = now,
            EndTime = now
        });
        _timeProvider.Advance(TimeSpan.FromSeconds(35)); // Beyond timeout

        LazarusServiceHealthCheck<TestService> healthCheck = new(_watchdogService, _timeProvider, CreateOptionsMonitor<TestService>(_timeout));

        HealthCheckResult result = await healthCheck.CheckHealthAsync(new());

        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
            await Assert.That(result.Description).Contains("Last heartbeat received too long ago");
            await Assert.That(result.Description).Contains("35");
        }
    }

    [Test]
    public async Task MultipleHeartbeatsUsesLatest()
    {
        DateTimeOffset firstTime = _timeProvider.GetUtcNow();
        _watchdogService.RegisterHeartbeat<TestService>(new()
        {
            StartTime = firstTime,
            EndTime = firstTime
        });
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        DateTimeOffset secondTime = _timeProvider.GetUtcNow();
        _watchdogService.RegisterHeartbeat<TestService>(new()
        {
            StartTime = secondTime,
            EndTime = secondTime
        }); // Second heartbeat
        _timeProvider.Advance(TimeSpan.FromSeconds(5));

        LazarusServiceHealthCheck<TestService> healthCheck = new(_watchdogService, _timeProvider, CreateOptionsMonitor<TestService>(_timeout));

        HealthCheckResult result = await healthCheck.CheckHealthAsync(new());
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
            // Should show 5 seconds (from latest heartbeat), not 15 seconds
            await Assert.That(result.Description).Contains("5");
            await Assert.That(result.Description).DoesNotContain("15");
        }
    }

    [Test]
    public async Task DifferentServiceTypesTrackedSeparately()
    {
        DateTimeOffset time1 = _timeProvider.GetUtcNow();
        _watchdogService.RegisterHeartbeat<Service1>(new()
        {
            StartTime = time1,
            EndTime = time1
        });
        _timeProvider.Advance(TimeSpan.FromSeconds(5));
        DateTimeOffset time2 = _timeProvider.GetUtcNow();
        _watchdogService.RegisterHeartbeat<Service2>(new()
        {
            StartTime = time2,
            EndTime = time2
        });
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        LazarusServiceHealthCheck<Service1> healthCheck1 = new(_watchdogService, _timeProvider, CreateOptionsMonitor<Service1>(_timeout));
        LazarusServiceHealthCheck<Service2> healthCheck2 = new(_watchdogService, _timeProvider, CreateOptionsMonitor<Service2>(_timeout));

        HealthCheckResult result1 = await healthCheck1.CheckHealthAsync(new());
        HealthCheckResult result2 = await healthCheck2.CheckHealthAsync(new());

        using (Assert.Multiple())
        {
            await Assert.That(result1.Status).IsEqualTo(HealthStatus.Healthy);
            await Assert.That(result1.Description).Contains("15"); // 5 + 10 seconds passed

            await Assert.That(result2.Status).IsEqualTo(HealthStatus.Healthy);
            await Assert.That(result2.Description).Contains("10"); // Only 10 seconds passed
        }
    }

    [Test]
    public async Task MetadataContainsExpectedFields()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        _watchdogService.RegisterHeartbeat<TestService>(new()
        {
            StartTime = now,
            EndTime = now
        });
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        LazarusServiceHealthCheck<TestService> healthCheck = new(_watchdogService, _timeProvider, CreateOptionsMonitor<TestService>(_timeout));

        HealthCheckResult result = await healthCheck.CheckHealthAsync(new());

        using (Assert.Multiple())
        {
            await Assert.That(result.Data).IsNotEmpty();
            await Assert.That(result.Data.ContainsKey("lastHeartbeat")).IsTrue();
            await Assert.That(result.Data.ContainsKey("timePassed")).IsTrue();
            await Assert.That(result.Data.ContainsKey("configuration")).IsTrue();
            await Assert.That(result.Data.ContainsKey("service")).IsTrue();
            await Assert.That(result.Data["service"]).IsEqualTo(nameof(TestService));

            LazarusHealthCheckConfiguration<TestService> config = (LazarusHealthCheckConfiguration<TestService>)result.Data["configuration"];
            await Assert.That(config.UnhealthyTimeSinceLastHeartbeat).IsEqualTo(_timeout);
        }
    }

    [Test]
    public async Task HealthyResultIncludesTimingInMessage()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        _watchdogService.RegisterHeartbeat<TestService>(new()
        {
            StartTime = now,
            EndTime = now
        });
        _timeProvider.Advance(TimeSpan.FromSeconds(15));

        LazarusServiceHealthCheck<TestService> healthCheck = new(_watchdogService, _timeProvider, CreateOptionsMonitor<TestService>(_timeout));

        HealthCheckResult result = await healthCheck.CheckHealthAsync(new());

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Description).Contains("15");
        await Assert.That(result.Description).Contains("s ago");
    }

    [Test]
    public async Task HeartbeatProgressesThroughHealthStates()
    {
        DateTimeOffset initialTime = _timeProvider.GetUtcNow();
        _watchdogService.RegisterHeartbeat<TestService>(new()
        {
            StartTime = initialTime,
            EndTime = initialTime
        });

        // Healthy state (10 seconds, within degraded threshold of 15s)
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        LazarusServiceHealthCheck<TestService> healthCheckHealthy = new(
            _watchdogService,
            _timeProvider,
            CreateOptionsMonitor<TestService>(_timeout));
        HealthCheckResult healthyResult = await healthCheckHealthy.CheckHealthAsync(new HealthCheckContext());

        using (Assert.Multiple())
        {
            await Assert.That(healthyResult.Status).IsEqualTo(HealthStatus.Healthy);
            await Assert.That(healthyResult.Description).Contains("Last heartbeat received in good time");
        }

        // Degraded state (20 seconds total, past degraded 15s threshold)
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        LazarusServiceHealthCheck<TestService> healthCheckDegraded = new(
            _watchdogService,
            _timeProvider,
            CreateOptionsMonitor<TestService>(_timeout));
        HealthCheckResult degradedResult = await healthCheckDegraded.CheckHealthAsync(new HealthCheckContext());

        using (Assert.Multiple())
        {
            await Assert.That(degradedResult.Status).IsEqualTo(HealthStatus.Degraded);
            await Assert.That(degradedResult.Description).Contains("Last heartbeat received too long ago");
            await Assert.That(degradedResult.Description).Contains("20");
        }

        // Unhealthy state (35 seconds total, past unhealthy 30s threshold)
        _timeProvider.Advance(TimeSpan.FromSeconds(15));
        LazarusServiceHealthCheck<TestService> healthCheckUnhealthy = new(
            _watchdogService,
            _timeProvider,
            CreateOptionsMonitor<TestService>(_timeout));
        HealthCheckResult unhealthyResult = await healthCheckUnhealthy.CheckHealthAsync(new HealthCheckContext());

        using (Assert.Multiple())
        {
            await Assert.That(unhealthyResult.Status).IsEqualTo(HealthStatus.Unhealthy);
            await Assert.That(unhealthyResult.Description).Contains("Last heartbeat received too long ago");
            await Assert.That(unhealthyResult.Description).Contains("35");
        }
    }

    private class FakeOptionsMonitor<TOptions> : Microsoft.Extensions.Options.IOptionsMonitor<TOptions>
    {
        private readonly TOptions _currentValue;

        public FakeOptionsMonitor(TOptions currentValue) => _currentValue = currentValue;

        public TOptions CurrentValue => _currentValue;
        public TOptions Get(string? name) => _currentValue;
        public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
    }

    private static Microsoft.Extensions.Options.IOptionsMonitor<LazarusHealthCheckConfiguration<TService>> CreateOptionsMonitor<TService>(
        TimeSpan unhealthyTimeout,
        TimeSpan? degradedTimeout = null)
    {
        LazarusHealthCheckConfiguration<TService> config = new()
        {
            UnhealthyTimeSinceLastHeartbeat = unhealthyTimeout,
            DegradedTimeSinceLastHeartbeat = degradedTimeout ?? unhealthyTimeout / 2,
            UnhealthyExceptionCountThreshold = 5,
            DegradedExceptionCountThreshold = 2,
            ExceptionCounterSlidingWindow = TimeSpan.FromMinutes(5)
        };

        return new FakeOptionsMonitor<LazarusHealthCheckConfiguration<TService>>(config);
    }

    private class TestService;
    private class Service1;
    private class Service2;
}
