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
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    public LazarusServiceHealthCheckTests()
    {
        _timeProvider = new();
    }

    [Test]
    public async Task NoHeartbeatReturnsUnhealthy()
    {
        InMemoryWatchdogService<TestService> watchdogService = new(_timeProvider, TimeSpan.FromMinutes(5));
        LazarusServiceHealthCheck<TestService> healthCheck = new(watchdogService, _timeProvider, CreateOptionsMonitor<TestService>(_timeout));

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
        InMemoryWatchdogService<TestService> watchdogService = new(_timeProvider, TimeSpan.FromMinutes(5));
        DateTimeOffset now = _timeProvider.GetUtcNow();
        watchdogService.RegisterHeartbeat(new()
        {
            StartTime = now,
            EndTime = now
        });
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        LazarusServiceHealthCheck<TestService> healthCheck = new(watchdogService, _timeProvider, CreateOptionsMonitor<TestService>(_timeout));

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
        InMemoryWatchdogService<TestService> watchdogService = new(_timeProvider, TimeSpan.FromMinutes(5));
        DateTimeOffset now = _timeProvider.GetUtcNow();
        watchdogService.RegisterHeartbeat(new()
        {
            StartTime = now,
            EndTime = now
        });
        _timeProvider.Advance(TimeSpan.FromSeconds(35)); // Beyond timeout

        LazarusServiceHealthCheck<TestService> healthCheck = new(watchdogService, _timeProvider, CreateOptionsMonitor<TestService>(_timeout));

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
        InMemoryWatchdogService<TestService> watchdogService = new(_timeProvider, TimeSpan.FromMinutes(5));
        DateTimeOffset firstTime = _timeProvider.GetUtcNow();
        watchdogService.RegisterHeartbeat(new()
        {
            StartTime = firstTime,
            EndTime = firstTime
        });
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        DateTimeOffset secondTime = _timeProvider.GetUtcNow();
        watchdogService.RegisterHeartbeat(new()
        {
            StartTime = secondTime,
            EndTime = secondTime
        }); // Second heartbeat
        _timeProvider.Advance(TimeSpan.FromSeconds(5));

        LazarusServiceHealthCheck<TestService> healthCheck = new(watchdogService, _timeProvider, CreateOptionsMonitor<TestService>(_timeout));

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
        InMemoryWatchdogService<Service1> watchdogService1 = new(_timeProvider, TimeSpan.FromMinutes(5));
        InMemoryWatchdogService<Service2> watchdogService2 = new(_timeProvider, TimeSpan.FromMinutes(5));

        DateTimeOffset time1 = _timeProvider.GetUtcNow();
        watchdogService1.RegisterHeartbeat(new()
        {
            StartTime = time1,
            EndTime = time1
        });
        _timeProvider.Advance(TimeSpan.FromSeconds(5));
        DateTimeOffset time2 = _timeProvider.GetUtcNow();
        watchdogService2.RegisterHeartbeat(new()
        {
            StartTime = time2,
            EndTime = time2
        });
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        LazarusServiceHealthCheck<Service1> healthCheck1 = new(watchdogService1, _timeProvider, CreateOptionsMonitor<Service1>(_timeout));
        LazarusServiceHealthCheck<Service2> healthCheck2 = new(watchdogService2, _timeProvider, CreateOptionsMonitor<Service2>(_timeout));

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
        InMemoryWatchdogService<TestService> watchdogService = new(_timeProvider, TimeSpan.FromMinutes(5));
        DateTimeOffset now = _timeProvider.GetUtcNow();
        watchdogService.RegisterHeartbeat(new()
        {
            StartTime = now,
            EndTime = now
        });
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        LazarusServiceHealthCheck<TestService> healthCheck = new(watchdogService, _timeProvider, CreateOptionsMonitor<TestService>(_timeout));

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
        InMemoryWatchdogService<TestService> watchdogService = new(_timeProvider, TimeSpan.FromMinutes(5));
        DateTimeOffset now = _timeProvider.GetUtcNow();
        watchdogService.RegisterHeartbeat(new()
        {
            StartTime = now,
            EndTime = now
        });
        _timeProvider.Advance(TimeSpan.FromSeconds(15));

        LazarusServiceHealthCheck<TestService> healthCheck = new(watchdogService, _timeProvider, CreateOptionsMonitor<TestService>(_timeout));

        HealthCheckResult result = await healthCheck.CheckHealthAsync(new());

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Description).Contains("15");
        await Assert.That(result.Description).Contains("s ago");
    }

    [Test]
    public async Task HeartbeatProgressesThroughHealthStates()
    {
        InMemoryWatchdogService<TestService> watchdogService = new(_timeProvider, TimeSpan.FromMinutes(5));
        DateTimeOffset initialTime = _timeProvider.GetUtcNow();
        watchdogService.RegisterHeartbeat(new()
        {
            StartTime = initialTime,
            EndTime = initialTime
        });

        // Healthy state (10 seconds, within degraded threshold of 15s)
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        LazarusServiceHealthCheck<TestService> healthCheckHealthy = new(
            watchdogService,
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
            watchdogService,
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
            watchdogService,
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

    [Test]
    public async Task FewExceptionsReturnHealthyWithTransientMessage()
    {
        InMemoryWatchdogService<TestService> watchdogService = new(_timeProvider, TimeSpan.FromMinutes(5));
        DateTimeOffset now = _timeProvider.GetUtcNow();

        // Register 1 heartbeat with exception (below degraded threshold of 2)
        watchdogService.RegisterHeartbeat(new()
        {
            StartTime = now,
            EndTime = now,
            Exception = new InvalidOperationException("Transient error")
        });
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        LazarusServiceHealthCheck<TestService> healthCheck = new(watchdogService, _timeProvider, CreateOptionsMonitor<TestService>(_timeout));
        HealthCheckResult result = await healthCheck.CheckHealthAsync(new());

        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
            await Assert.That(result.Description).Contains("likely transient");
            await Assert.That(result.Data["exceptionsInWindow"]).IsEqualTo(1);
        }
    }

    [Test]
    public async Task ManyExceptionsReturnDegraded()
    {
        InMemoryWatchdogService<TestService> watchdogService = new(_timeProvider, TimeSpan.FromMinutes(5));
        DateTimeOffset now = _timeProvider.GetUtcNow();

        // Register 3 heartbeats with exceptions (between degraded threshold 2 and unhealthy threshold 5)
        for (int i = 0; i < 3; i++)
        {
            watchdogService.RegisterHeartbeat(new()
            {
                StartTime = now,
                EndTime = now,
                Exception = new InvalidOperationException($"Error {i}")
            });
            _timeProvider.Advance(TimeSpan.FromSeconds(10));
        }

        LazarusServiceHealthCheck<TestService> healthCheck = new(watchdogService, _timeProvider, CreateOptionsMonitor<TestService>(_timeout));
        HealthCheckResult result = await healthCheck.CheckHealthAsync(new());

        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(HealthStatus.Degraded);
            await Assert.That(result.Description).Contains("3");
            await Assert.That(result.Data["exceptionsInWindow"]).IsEqualTo(3);
        }
    }

    [Test]
    public async Task TooManyExceptionsReturnUnhealthy()
    {
        InMemoryWatchdogService<TestService> watchdogService = new(_timeProvider, TimeSpan.FromMinutes(5));
        DateTimeOffset now = _timeProvider.GetUtcNow();

        // Register 6 heartbeats with exceptions (above unhealthy threshold of 5)
        for (int i = 0; i < 6; i++)
        {
            watchdogService.RegisterHeartbeat(new()
            {
                StartTime = now,
                EndTime = now,
                Exception = new InvalidOperationException($"Error {i}")
            });
            _timeProvider.Advance(TimeSpan.FromSeconds(10));
        }

        LazarusServiceHealthCheck<TestService> healthCheck = new(watchdogService, _timeProvider, CreateOptionsMonitor<TestService>(_timeout));
        HealthCheckResult result = await healthCheck.CheckHealthAsync(new());

        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
            await Assert.That(result.Description).Contains("6");
            await Assert.That(result.Data["exceptionsInWindow"]).IsEqualTo(6);
        }
    }

    [Test]
    public async Task OldExceptionsDontAffectHealth()
    {
        InMemoryWatchdogService<TestService> watchdogService = new(_timeProvider, TimeSpan.FromMinutes(5));
        DateTimeOffset startTime = _timeProvider.GetUtcNow();

        // Register 10 exceptions
        for (int i = 0; i < 10; i++)
        {
            watchdogService.RegisterHeartbeat(new()
            {
                StartTime = startTime,
                EndTime = startTime,
                Exception = new InvalidOperationException($"Old error {i}")
            });
            _timeProvider.Advance(TimeSpan.FromSeconds(10));
        }

        // Advance time so all exceptions are outside the 5-minute window
        _timeProvider.Advance(TimeSpan.FromMinutes(6));

        // Register a recent heartbeat without exception
        DateTimeOffset recentTime = _timeProvider.GetUtcNow();
        watchdogService.RegisterHeartbeat(new()
        {
            StartTime = recentTime,
            EndTime = recentTime,
            Exception = null
        });

        LazarusServiceHealthCheck<TestService> healthCheck = new(watchdogService, _timeProvider, CreateOptionsMonitor<TestService>(_timeout));
        HealthCheckResult result = await healthCheck.CheckHealthAsync(new());

        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
            await Assert.That(result.Data["exceptionsInWindow"]).IsEqualTo(0);
        }
    }

    [Test]
    public async Task ExceptionCountAppearsInMetadata()
    {
        InMemoryWatchdogService<TestService> watchdogService = new(_timeProvider, TimeSpan.FromMinutes(5));
        DateTimeOffset now = _timeProvider.GetUtcNow();

        // Register 4 heartbeats with exceptions
        for (int i = 0; i < 4; i++)
        {
            watchdogService.RegisterHeartbeat(new()
            {
                StartTime = now,
                EndTime = now,
                Exception = new InvalidOperationException($"Error {i}")
            });
            _timeProvider.Advance(TimeSpan.FromSeconds(10));
        }

        LazarusServiceHealthCheck<TestService> healthCheck = new(watchdogService, _timeProvider, CreateOptionsMonitor<TestService>(_timeout));
        HealthCheckResult result = await healthCheck.CheckHealthAsync(new());

        using (Assert.Multiple())
        {
            await Assert.That(result.Data.ContainsKey("exceptionsInWindow")).IsTrue();
            await Assert.That(result.Data["exceptionsInWindow"]).IsEqualTo(4);
            await Assert.That(result.Data.ContainsKey("exceptionsStatus")).IsTrue();
        }
    }

    [Test]
    public async Task DifferentServicesWithDifferentExceptionThresholdsReportDifferentStatus()
    {
        InMemoryWatchdogService<Service1> watchdog1 = new(_timeProvider, TimeSpan.FromMinutes(5));
        InMemoryWatchdogService<Service2> watchdog2 = new(_timeProvider, TimeSpan.FromMinutes(5));
        DateTimeOffset now = _timeProvider.GetUtcNow();

        // Both services get 3 exceptions
        for (int i = 0; i < 3; i++)
        {
            watchdog1.RegisterHeartbeat(new()
            {
                StartTime = now,
                EndTime = now,
                Exception = new InvalidOperationException($"Error {i}")
            });
            watchdog2.RegisterHeartbeat(new()
            {
                StartTime = now,
                EndTime = now,
                Exception = new InvalidOperationException($"Error {i}")
            });
            _timeProvider.Advance(TimeSpan.FromSeconds(10));
        }

        // Service1 has degraded threshold of 2, unhealthy threshold of 5
        LazarusHealthCheckConfiguration<Service1> config1 = new()
        {
            UnhealthyTimeSinceLastHeartbeat = TimeSpan.FromMinutes(10),
            DegradedTimeSinceLastHeartbeat = TimeSpan.FromMinutes(5),
            UnhealthyExceptionCountThreshold = 5,
            DegradedExceptionCountThreshold = 2,
            ExceptionCounterSlidingWindow = TimeSpan.FromMinutes(5)
        };

        // Service2 has degraded threshold of 5, unhealthy threshold of 10
        LazarusHealthCheckConfiguration<Service2> config2 = new()
        {
            UnhealthyTimeSinceLastHeartbeat = TimeSpan.FromMinutes(10),
            DegradedTimeSinceLastHeartbeat = TimeSpan.FromMinutes(5),
            UnhealthyExceptionCountThreshold = 10,
            DegradedExceptionCountThreshold = 5,
            ExceptionCounterSlidingWindow = TimeSpan.FromMinutes(5)
        };

        LazarusServiceHealthCheck<Service1> healthCheck1 = new(watchdog1, _timeProvider, new FakeOptionsMonitor<LazarusHealthCheckConfiguration<Service1>>(config1));
        LazarusServiceHealthCheck<Service2> healthCheck2 = new(watchdog2, _timeProvider, new FakeOptionsMonitor<LazarusHealthCheckConfiguration<Service2>>(config2));

        HealthCheckResult result1 = await healthCheck1.CheckHealthAsync(new());
        HealthCheckResult result2 = await healthCheck2.CheckHealthAsync(new());

        using (Assert.Multiple())
        {
            await Assert.That(result1.Status).IsEqualTo(HealthStatus.Degraded);
            await Assert.That(result1.Description).Contains("3");
            await Assert.That(result2.Status).IsEqualTo(HealthStatus.Healthy);
            await Assert.That(result2.Description).Contains("3");
        }
    }

    [Test]
    public async Task DifferentServicesWithDifferentHeartbeatTimeoutsReportDifferentStatus()
    {
        InMemoryWatchdogService<Service1> watchdog1 = new(_timeProvider, TimeSpan.FromMinutes(5));
        InMemoryWatchdogService<Service2> watchdog2 = new(_timeProvider, TimeSpan.FromMinutes(5));
        DateTimeOffset now = _timeProvider.GetUtcNow();

        // Both services get a heartbeat at the same time
        watchdog1.RegisterHeartbeat(new() { StartTime = now, EndTime = now, Exception = null });
        watchdog2.RegisterHeartbeat(new() { StartTime = now, EndTime = now, Exception = null });

        // Advance time by 45 seconds
        _timeProvider.Advance(TimeSpan.FromSeconds(45));

        // Service1 has unhealthy timeout of 30s, degraded timeout of 15s
        LazarusHealthCheckConfiguration<Service1> config1 = new()
        {
            UnhealthyTimeSinceLastHeartbeat = TimeSpan.FromSeconds(30),
            DegradedTimeSinceLastHeartbeat = TimeSpan.FromSeconds(15),
            UnhealthyExceptionCountThreshold = 5,
            DegradedExceptionCountThreshold = 2,
            ExceptionCounterSlidingWindow = TimeSpan.FromMinutes(5)
        };

        // Service2 has unhealthy timeout of 60s, degraded timeout of 30s
        LazarusHealthCheckConfiguration<Service2> config2 = new()
        {
            UnhealthyTimeSinceLastHeartbeat = TimeSpan.FromSeconds(60),
            DegradedTimeSinceLastHeartbeat = TimeSpan.FromSeconds(30),
            UnhealthyExceptionCountThreshold = 5,
            DegradedExceptionCountThreshold = 2,
            ExceptionCounterSlidingWindow = TimeSpan.FromMinutes(5)
        };

        LazarusServiceHealthCheck<Service1> healthCheck1 = new(watchdog1, _timeProvider, new FakeOptionsMonitor<LazarusHealthCheckConfiguration<Service1>>(config1));
        LazarusServiceHealthCheck<Service2> healthCheck2 = new(watchdog2, _timeProvider, new FakeOptionsMonitor<LazarusHealthCheckConfiguration<Service2>>(config2));

        HealthCheckResult result1 = await healthCheck1.CheckHealthAsync(new());
        HealthCheckResult result2 = await healthCheck2.CheckHealthAsync(new());

        using (Assert.Multiple())
        {
            // Service1: 45s since heartbeat, unhealthy threshold is 30s -> Unhealthy
            await Assert.That(result1.Status).IsEqualTo(HealthStatus.Unhealthy);
            // Service2: 45s since heartbeat, degraded threshold is 30s, unhealthy is 60s -> Degraded
            await Assert.That(result2.Status).IsEqualTo(HealthStatus.Degraded);
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
