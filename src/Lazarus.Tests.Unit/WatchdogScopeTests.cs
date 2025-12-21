using Lazarus.Internal.Watchdog;
using Lazarus.Public.Watchdog;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Lazarus.Tests.Unit;

public class WatchdogScopeTests
{
    private readonly FakeTimeProvider _timeProvider;
    private readonly InMemoryWatchdogService<TestService> _watchdogService;

    public WatchdogScopeTests()
    {
        _timeProvider = new();
        _watchdogService = new(_timeProvider, TimeSpan.FromMinutes(5));
    }

    [Test]
    public async Task RegistersHeartbeatWithCorrectTiming()
    {
        DateTimeOffset initialTime = _timeProvider.GetUtcNow();
        WatchdogScope<TestService> scope = new(
            NullLogger<WatchdogScope<TestService>>.Instance,
            _timeProvider,
            _watchdogService
        );

        using (scope)
        {
            await scope.ExecuteAsync(async () => await Task.CompletedTask);
        }

        Heartbeat? heartbeat = _watchdogService.GetLastHeartbeat();
        using (Assert.Multiple())
        {
            await Assert.That(heartbeat).IsNotNull();
            await Assert.That(heartbeat!.StartTime).IsEqualTo(initialTime);
            await Assert.That(heartbeat.EndTime).IsGreaterThanOrEqualTo(initialTime);
            await Assert.That(heartbeat.Exception).IsNull();
        }
    }

    [Test]
    public async Task CapturesExceptionAndRethrows()
    {
        InvalidOperationException expectedException = new("Test exception");
        WatchdogScope<TestService> scope = new(
            NullLogger<WatchdogScope<TestService>>.Instance,
            _timeProvider,
            _watchdogService
        );

        InvalidOperationException? thrownException = null;
        try
        {
            using (scope)
            {
                await scope.ExecuteAsync(() => throw expectedException);
            }
        }
        catch (InvalidOperationException ex)
        {
            thrownException = ex;
        }

        Heartbeat? heartbeat = _watchdogService.GetLastHeartbeat();
        using (Assert.Multiple())
        {
            await Assert.That(thrownException).IsEqualTo(expectedException);
            await Assert.That(heartbeat).IsNotNull();
            await Assert.That(heartbeat!.Exception).IsEqualTo(expectedException);
        }
    }

    [Test]
    public async Task RegistersHeartbeatWithExceptionDetails()
    {
        InvalidOperationException expectedException = new("Specific test exception");
        DateTimeOffset initialTime = _timeProvider.GetUtcNow();
        WatchdogScope<TestService> scope = new(
            NullLogger<WatchdogScope<TestService>>.Instance,
            _timeProvider,
            _watchdogService
        );

        try
        {
            using (scope)
            {
                await scope.ExecuteAsync(() => throw expectedException);
            }
        }
        catch
        {
            // Expected
        }

        Heartbeat? heartbeat = _watchdogService.GetLastHeartbeat();
        using (Assert.Multiple())
        {
            await Assert.That(heartbeat).IsNotNull();
            await Assert.That(heartbeat!.Exception).IsEqualTo(expectedException);
            await Assert.That(heartbeat.Exception!.Message).IsEqualTo("Specific test exception");
            await Assert.That(heartbeat.StartTime).IsEqualTo(initialTime);
            await Assert.That(heartbeat.EndTime).IsGreaterThanOrEqualTo(initialTime);
        }
    }

    [Test]
    public async Task ThrowsWhenCalledTwice()
    {
        WatchdogScope<TestService> scope = new(
            NullLogger<WatchdogScope<TestService>>.Instance,
            _timeProvider,
            _watchdogService
        );

        await scope.ExecuteAsync(async () => await Task.CompletedTask);

        InvalidOperationException? exception = null;
        try
        {
            await scope.ExecuteAsync(async () => await Task.CompletedTask);
        }
        catch (InvalidOperationException ex)
        {
            exception = ex;
        }

        using (Assert.Multiple())
        {
            await Assert.That(exception).IsNotNull();
            await Assert.That(exception!.Message).Contains("already been run");
        }
    }

    [Test]
    public async Task SkipsHeartbeatWhenDisposedWithoutExecuting()
    {
        WatchdogScope<TestService> scope = new(
            NullLogger<WatchdogScope<TestService>>.Instance,
            _timeProvider,
            _watchdogService
        );

        scope.Dispose();

        Heartbeat? heartbeat = _watchdogService.GetLastHeartbeat();
        await Assert.That(heartbeat).IsNull();
    }

    [Test]
    public async Task IgnoresSecondDisposal()
    {
        WatchdogScope<TestService> scope = new(
            NullLogger<WatchdogScope<TestService>>.Instance,
            _timeProvider,
            _watchdogService
        );

        await scope.ExecuteAsync(async () => await Task.CompletedTask);

        scope.Dispose();
        Heartbeat? firstHeartbeat = _watchdogService.GetLastHeartbeat();
        scope.Dispose();
        Heartbeat? secondHeartbeat = _watchdogService.GetLastHeartbeat();

        using (Assert.Multiple())
        {
            await Assert.That(firstHeartbeat).IsNotNull();
            await Assert.That(secondHeartbeat).IsEqualTo(firstHeartbeat);
        }
    }

    [Test]
    public async Task CapturesTimingAccurately()
    {
        DateTimeOffset initialTime = _timeProvider.GetUtcNow();
        WatchdogScope<TestService> scope = new(
            NullLogger<WatchdogScope<TestService>>.Instance,
            _timeProvider,
            _watchdogService
        );

        using (scope)
        {
            await scope.ExecuteAsync(async () =>
            {
                _timeProvider.Advance(TimeSpan.FromSeconds(5));
                await Task.CompletedTask;
            });
        }

        Heartbeat? heartbeat = _watchdogService.GetLastHeartbeat();
        using (Assert.Multiple())
        {
            await Assert.That(heartbeat).IsNotNull();
            await Assert.That(heartbeat!.StartTime).IsEqualTo(initialTime);
            await Assert.That(heartbeat.EndTime).IsGreaterThan(heartbeat.StartTime);
            await Assert.That(heartbeat.EndTime - heartbeat.StartTime).IsGreaterThanOrEqualTo(TimeSpan.FromSeconds(5));
        }
    }

    [Test]
    public async Task RecordsEndTimeAtDisposal()
    {
        DateTimeOffset initialTime = _timeProvider.GetUtcNow();
        WatchdogScope<TestService> scope = new(
            NullLogger<WatchdogScope<TestService>>.Instance,
            _timeProvider,
            _watchdogService
        );

        using (scope)
        {
            await scope.ExecuteAsync(async () =>
            {
                _timeProvider.Advance(TimeSpan.FromSeconds(10));
                await Task.CompletedTask;
            });
            _timeProvider.Advance(TimeSpan.FromSeconds(2));
        }

        Heartbeat? heartbeat = _watchdogService.GetLastHeartbeat();
        using (Assert.Multiple())
        {
            await Assert.That(heartbeat).IsNotNull();
            await Assert.That(heartbeat!.StartTime).IsEqualTo(initialTime);
            await Assert.That(heartbeat.EndTime).IsEqualTo(initialTime + TimeSpan.FromSeconds(12));
        }
    }

    [Test]
    public async Task TracksDifferentServiceTypesSeparately()
    {
        DateTimeOffset timeA = _timeProvider.GetUtcNow();
        InMemoryWatchdogService<TestServiceA> watchdogA = new(_timeProvider, TimeSpan.FromMinutes(5));
        WatchdogScope<TestServiceA> scopeA = new(
            NullLogger<WatchdogScope<TestServiceA>>.Instance,
            _timeProvider,
            watchdogA
        );

        using (scopeA)
        {
            await scopeA.ExecuteAsync(async () => await Task.CompletedTask);
        }

        _timeProvider.Advance(TimeSpan.FromSeconds(5));
        DateTimeOffset timeB = _timeProvider.GetUtcNow();
        InMemoryWatchdogService<TestServiceB> watchdogB = new(_timeProvider, TimeSpan.FromMinutes(5));
        WatchdogScope<TestServiceB> scopeB = new(
            NullLogger<WatchdogScope<TestServiceB>>.Instance,
            _timeProvider,
            watchdogB
        );

        using (scopeB)
        {
            await scopeB.ExecuteAsync(async () => await Task.CompletedTask);
        }

        Heartbeat? heartbeatA = watchdogA.GetLastHeartbeat();
        Heartbeat? heartbeatB = watchdogB.GetLastHeartbeat();

        using (Assert.Multiple())
        {
            await Assert.That(heartbeatA).IsNotNull();
            await Assert.That(heartbeatB).IsNotNull();
            await Assert.That(heartbeatA!.StartTime).IsEqualTo(timeA);
            await Assert.That(heartbeatB!.StartTime).IsEqualTo(timeB);
            await Assert.That(heartbeatA).IsNotEqualTo(heartbeatB);
        }
    }

    [Test]
    public async Task CompletesAsyncActions()
    {
        WatchdogScope<TestService> scope = new(
            NullLogger<WatchdogScope<TestService>>.Instance,
            _timeProvider,
            _watchdogService
        );

        bool actionCompleted = false;

        using (scope)
        {
            await scope.ExecuteAsync(async () =>
            {
                await Task.Delay(1);
                actionCompleted = true;
            });
        }

        Heartbeat? heartbeat = _watchdogService.GetLastHeartbeat();
        using (Assert.Multiple())
        {
            await Assert.That(actionCompleted).IsTrue();
            await Assert.That(heartbeat).IsNotNull();
            await Assert.That(heartbeat!.Exception).IsNull();
        }
    }

    [Test]
    public async Task HandlesCancellation()
    {
        WatchdogScope<TestService> scope = new(
            NullLogger<WatchdogScope<TestService>>.Instance,
            _timeProvider,
            _watchdogService
        );

        CancellationTokenSource cts = new();
        cts.Cancel();

        OperationCanceledException? exception = null;
        try
        {
            using (scope)
            {
                await scope.ExecuteAsync(async () =>
                {
                    await Task.Delay(1000, cts.Token);
                });
            }
        }
        catch (OperationCanceledException ex)
        {
            exception = ex;
        }

        Heartbeat? heartbeat = _watchdogService.GetLastHeartbeat();
        using (Assert.Multiple())
        {
            await Assert.That(exception).IsNotNull();
            await Assert.That(heartbeat).IsNotNull();
            await Assert.That(heartbeat!.Exception).IsEqualTo(exception);
        }
    }

    private class TestService { }
    private class TestServiceA { }
    private class TestServiceB { }
}
