using Lazarus.Internal.Watchdog;
using Microsoft.Extensions.Time.Testing;

namespace Lazarus.Tests.Unit;

public class WatchdogServiceTests
{
    private readonly WatchdogService<string> _watchdog;
    private readonly FakeTimeProvider _timeProvider;

    public WatchdogServiceTests()
    {
        _timeProvider = new();
        _watchdog = new(_timeProvider);
    }

    [Test]
    public async Task GetLastHeartbeatReturnsNullWhenServiceNotRegistered()
    {
        DateTimeOffset? result = _watchdog.GetLastHeartbeat("unknown");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task RegisterHeartbeatStoresCurrentTime()
    {
        DateTimeOffset expectedTime = _timeProvider.GetUtcNow();

        _watchdog.RegisterHeartbeat("service1");

        await Assert.That(_watchdog.GetLastHeartbeat("service1")).IsEqualTo(expectedTime);
    }

    [Test]
    public async Task RegisterHeartbeatUpdatesTimeWhenCalledAgain()
    {
        _watchdog.RegisterHeartbeat("service1");
        DateTimeOffset firstTime = _watchdog.GetLastHeartbeat("service1")!.Value;

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        _watchdog.RegisterHeartbeat("service1");

        await Assert.That(_watchdog.GetLastHeartbeat("service1")).IsEqualTo(firstTime + TimeSpan.FromMinutes(5));
    }


    [Test]
    public async Task TracksMultipleServicesIndependently()
    {
        _watchdog.RegisterHeartbeat("service1");
        DateTimeOffset time1 = _timeProvider.GetUtcNow();

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        _watchdog.RegisterHeartbeat("service2");
        DateTimeOffset time2 = _timeProvider.GetUtcNow();

        await Assert.That(_watchdog.GetLastHeartbeat("service1")).IsEqualTo(time1);
        await Assert.That(_watchdog.GetLastHeartbeat("service2")).IsEqualTo(time2);
    }
}
