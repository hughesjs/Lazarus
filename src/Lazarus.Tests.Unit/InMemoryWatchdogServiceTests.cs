using Lazarus.Internal.Watchdog;
using Lazarus.Public.Watchdog;
using Microsoft.Extensions.Time.Testing;

namespace Lazarus.Tests.Unit;

public class InMemoryWatchdogServiceTests
{
    private readonly InMemoryWatchdogService _inMemoryWatchdog;
    private readonly FakeTimeProvider _timeProvider;

    public InMemoryWatchdogServiceTests()
    {
        _timeProvider = new();
        _inMemoryWatchdog = new(_timeProvider);
    }

    [Test]
    public async Task GetLastHeartbeatReturnsNullWhenServiceNotRegistered()
    {
        Heartbeat? result = _inMemoryWatchdog.GetLastHeartbeat<UnknownService>();

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task RegisterHeartbeatStoresCurrentTime()
    {
        DateTimeOffset expectedTime = _timeProvider.GetUtcNow();
        Heartbeat heartbeat = new() { StartTime = expectedTime, EndTime = expectedTime };

        _inMemoryWatchdog.RegisterHeartbeat<Service1>(heartbeat);

        await Assert.That(_inMemoryWatchdog.GetLastHeartbeat<Service1>()!.StartTime).IsEqualTo(expectedTime);
    }

    [Test]
    public async Task RegisterHeartbeatUpdatesTimeWhenCalledAgain()
    {
        DateTimeOffset firstTime = _timeProvider.GetUtcNow();
        Heartbeat firstHeartbeat = new() { StartTime = firstTime, EndTime = firstTime };
        _inMemoryWatchdog.RegisterHeartbeat<Service1>(firstHeartbeat);

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        DateTimeOffset secondTime = _timeProvider.GetUtcNow();
        Heartbeat secondHeartbeat = new() { StartTime = secondTime, EndTime = secondTime };
        _inMemoryWatchdog.RegisterHeartbeat<Service1>(secondHeartbeat);

        await Assert.That(_inMemoryWatchdog.GetLastHeartbeat<Service1>()!.StartTime).IsEqualTo(firstTime + TimeSpan.FromMinutes(5));
    }


    [Test]
    public async Task TracksMultipleServicesIndependently()
    {
        DateTimeOffset time1 = _timeProvider.GetUtcNow();
        Heartbeat heartbeat1 = new() { StartTime = time1, EndTime = time1 };
        _inMemoryWatchdog.RegisterHeartbeat<Service1>(heartbeat1);

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        DateTimeOffset time2 = _timeProvider.GetUtcNow();
        Heartbeat heartbeat2 = new() { StartTime = time2, EndTime = time2 };
        _inMemoryWatchdog.RegisterHeartbeat<Service2>(heartbeat2);

        await Assert.That(_inMemoryWatchdog.GetLastHeartbeat<Service1>()!.StartTime).IsEqualTo(time1);
        await Assert.That(_inMemoryWatchdog.GetLastHeartbeat<Service2>()!.StartTime).IsEqualTo(time2);
    }

    private class UnknownService;
    private class Service1;
    private class Service2;
}
