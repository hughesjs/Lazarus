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
        InMemoryWatchdogService<UnknownService> watchdog = new(_timeProvider);
        Heartbeat? result = watchdog.GetLastHeartbeat();

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task RegisterHeartbeatStoresCurrentTime()
    {
        InMemoryWatchdogService<Service1> watchdog = new(_timeProvider);
        DateTimeOffset expectedTime = _timeProvider.GetUtcNow();
        Heartbeat heartbeat = new() { StartTime = expectedTime, EndTime = expectedTime };

        watchdog.RegisterHeartbeat(heartbeat);

        await Assert.That(watchdog.GetLastHeartbeat()!.StartTime).IsEqualTo(expectedTime);
    }

    [Test]
    public async Task RegisterHeartbeatUpdatesTimeWhenCalledAgain()
    {
        InMemoryWatchdogService<Service1> watchdog = new(_timeProvider);
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
        InMemoryWatchdogService<Service1> watchdog1 = new(_timeProvider);
        InMemoryWatchdogService<Service2> watchdog2 = new(_timeProvider);

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

    private class UnknownService;
    private class Service1;
    private class Service2;
}
