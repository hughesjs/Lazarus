using Lazarus.Internal.Watchdog;
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
        DateTimeOffset? result = _inMemoryWatchdog.GetLastHeartbeat<UnknownService>();

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task RegisterHeartbeatStoresCurrentTime()
    {
        DateTimeOffset expectedTime = _timeProvider.GetUtcNow();

        _inMemoryWatchdog.RegisterHeartbeat<Service1>();

        await Assert.That(_inMemoryWatchdog.GetLastHeartbeat<Service1>()).IsEqualTo(expectedTime);
    }

    [Test]
    public async Task RegisterHeartbeatUpdatesTimeWhenCalledAgain()
    {
        _inMemoryWatchdog.RegisterHeartbeat<Service1>();
        DateTimeOffset firstTime = _inMemoryWatchdog.GetLastHeartbeat<Service1>()!.Value;

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        _inMemoryWatchdog.RegisterHeartbeat<Service1>();

        await Assert.That(_inMemoryWatchdog.GetLastHeartbeat<Service1>()).IsEqualTo(firstTime + TimeSpan.FromMinutes(5));
    }


    [Test]
    public async Task TracksMultipleServicesIndependently()
    {
        _inMemoryWatchdog.RegisterHeartbeat<Service1>();
        DateTimeOffset time1 = _timeProvider.GetUtcNow();

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        _inMemoryWatchdog.RegisterHeartbeat<Service2>();
        DateTimeOffset time2 = _timeProvider.GetUtcNow();

        await Assert.That(_inMemoryWatchdog.GetLastHeartbeat<Service1>()).IsEqualTo(time1);
        await Assert.That(_inMemoryWatchdog.GetLastHeartbeat<Service2>()).IsEqualTo(time2);
    }

    private class UnknownService;
    private class Service1;
    private class Service2;
}
