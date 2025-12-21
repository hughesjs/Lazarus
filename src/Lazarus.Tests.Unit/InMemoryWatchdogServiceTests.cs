using Lazarus.Internal.Watchdog;
using Lazarus.Public.Watchdog;
using Microsoft.Extensions.Time.Testing;

namespace Lazarus.Tests.Unit;

public class InMemoryWatchdogServiceTests
{
    private readonly FakeTimeProvider _timeProvider;

    public InMemoryWatchdogServiceTests()
    {
        _timeProvider = new();
    }

    [Test]
    public async Task GetLastHeartbeatReturnsNullWhenServiceNotRegistered()
    {
        InMemoryWatchdogService<UnknownService> watchdog = new(_timeProvider, TimeSpan.FromMinutes(5));
        Heartbeat? result = watchdog.GetLastHeartbeat();

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task RegisterHeartbeatStoresCurrentTime()
    {
        InMemoryWatchdogService<Service1> watchdog = new(_timeProvider, TimeSpan.FromMinutes(5));
        DateTimeOffset expectedTime = _timeProvider.GetUtcNow();
        Heartbeat heartbeat = new() { StartTime = expectedTime, EndTime = expectedTime };

        watchdog.RegisterHeartbeat(heartbeat);

        await Assert.That(watchdog.GetLastHeartbeat()!.StartTime).IsEqualTo(expectedTime);
    }

    [Test]
    public async Task RegisterHeartbeatUpdatesTimeWhenCalledAgain()
    {
        InMemoryWatchdogService<Service1> watchdog = new(_timeProvider, TimeSpan.FromMinutes(5));
        DateTimeOffset firstTime = _timeProvider.GetUtcNow();
        Heartbeat firstHeartbeat = new() { StartTime = firstTime, EndTime = firstTime };
        watchdog.RegisterHeartbeat(firstHeartbeat);

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        DateTimeOffset secondTime = _timeProvider.GetUtcNow();
        Heartbeat secondHeartbeat = new() { StartTime = secondTime, EndTime = secondTime };
        watchdog.RegisterHeartbeat(secondHeartbeat);

        await Assert.That(watchdog.GetLastHeartbeat()!.StartTime).IsEqualTo(firstTime + TimeSpan.FromMinutes(5));
    }


    [Test]
    public async Task TracksMultipleServicesIndependently()
    {
        InMemoryWatchdogService<Service1> watchdog1 = new(_timeProvider, TimeSpan.FromMinutes(5));
        InMemoryWatchdogService<Service2> watchdog2 = new(_timeProvider, TimeSpan.FromMinutes(5));

        DateTimeOffset time1 = _timeProvider.GetUtcNow();
        Heartbeat heartbeat1 = new() { StartTime = time1, EndTime = time1 };
        watchdog1.RegisterHeartbeat(heartbeat1);

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        DateTimeOffset time2 = _timeProvider.GetUtcNow();
        Heartbeat heartbeat2 = new() { StartTime = time2, EndTime = time2 };
        watchdog2.RegisterHeartbeat(heartbeat2);

        await Assert.That(watchdog1.GetLastHeartbeat()!.StartTime).IsEqualTo(time1);
        await Assert.That(watchdog2.GetLastHeartbeat()!.StartTime).IsEqualTo(time2);
    }

    [Test]
    public async Task GetExceptionsInWindowReturnsEmptyWhenNoExceptions()
    {
        InMemoryWatchdogService<Service1> watchdog = new(_timeProvider, TimeSpan.FromMinutes(5));
        DateTimeOffset now = _timeProvider.GetUtcNow();

        watchdog.RegisterHeartbeat(new() { StartTime = now, EndTime = now, Exception = null });
        watchdog.RegisterHeartbeat(new() { StartTime = now, EndTime = now, Exception = null });

        IReadOnlyList<Exception> exceptions = watchdog.GetExceptionsInWindow();

        await Assert.That(exceptions).IsEmpty();
    }

    [Test]
    public async Task GetExceptionsInWindowReturnsOnlyExceptionsInWindow()
    {
        InMemoryWatchdogService<Service1> watchdog = new(_timeProvider, TimeSpan.FromMinutes(5));

        // Register 5 heartbeats with exceptions over 10 minutes
        for (int i = 0; i < 5; i++)
        {
            DateTimeOffset currentTime = _timeProvider.GetUtcNow();
            watchdog.RegisterHeartbeat(new()
            {
                StartTime = currentTime,
                EndTime = currentTime,
                Exception = new InvalidOperationException($"Error {i}")
            });
            _timeProvider.Advance(TimeSpan.FromMinutes(2));
        }

        // Now we're at initial time + 10 minutes
        // Heartbeats are at: 0min, 2min, 4min, 6min, 8min
        // Window is 5 minutes, cutoff is 10min - 5min = 5min
        // Heartbeats with EndTime > 5min: 6min and 8min (Error 3 and Error 4)
        IReadOnlyList<Exception> exceptions = watchdog.GetExceptionsInWindow();

        using (Assert.Multiple())
        {
            await Assert.That(exceptions).Count().IsEqualTo(2);
            await Assert.That(exceptions[0].Message).IsEqualTo("Error 3");
            await Assert.That(exceptions[1].Message).IsEqualTo("Error 4");
        }
    }

    [Test]
    public async Task GetExceptionsInWindowPrunesOldHeartbeats()
    {
        InMemoryWatchdogService<Service1> watchdog = new(_timeProvider, TimeSpan.FromMinutes(5));
        DateTimeOffset startTime = _timeProvider.GetUtcNow();

        // Register heartbeats spanning 10 minutes
        for (int i = 0; i < 5; i++)
        {
            DateTimeOffset currentTime = _timeProvider.GetUtcNow();
            watchdog.RegisterHeartbeat(new()
            {
                StartTime = currentTime,
                EndTime = currentTime,
                Exception = i % 2 == 0 ? new InvalidOperationException($"Error {i}") : null
            });
            _timeProvider.Advance(TimeSpan.FromMinutes(2));
        }

        // Call GetExceptionsInWindow() which should prune old heartbeats
        watchdog.GetExceptionsInWindow();

        // GetLastHeartbeat() should still work and return the last heartbeat within the window
        // Heartbeats at 0, 2, 4 min are pruned (older than window start at 5min)
        // Heartbeats at 6, 8 min remain
        Heartbeat? lastHeartbeat = watchdog.GetLastHeartbeat();

        using (Assert.Multiple())
        {
            await Assert.That(lastHeartbeat).IsNotNull();
            // Last heartbeat should be at 8 minutes (the most recent one within the window)
            await Assert.That(lastHeartbeat!.EndTime).IsEqualTo(startTime + TimeSpan.FromMinutes(8));
            // Verify it's within the 5-minute window (> 5min from start)
            await Assert.That(lastHeartbeat.EndTime).IsGreaterThan(startTime + TimeSpan.FromMinutes(5));
        }
    }

    private class UnknownService;
    private class Service1;
    private class Service2;
}
