using Lazarus.Internal.Service;
using Lazarus.Internal.Watchdog;
using Lazarus.Public;
using Lazarus.Public.Watchdog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Lazarus.Tests.Unit;

public class LazarusServiceTests : IAsyncDisposable
{
    private readonly LazarusService<TestService> _ts;
    private readonly TestService _innerService;
    private readonly FakeTimeProvider _tp;
    private readonly IWatchdogService<TestService> _watchdog;
    private readonly CancellationToken _ctx;

    private readonly TimeSpan _loopTime = TimeSpan.FromSeconds(5);

    public LazarusServiceTests()
    {
        _tp = new();
        _watchdog = new InMemoryWatchdogService<TestService>(_tp, TimeSpan.FromMinutes(5));
        _innerService = new();

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IWatchdogService<TestService>>(_watchdog);
        IServiceProvider serviceProvider = services.BuildServiceProvider();
        WatchdogScopeFactory watchdogScopeFactory = new(serviceProvider, _tp);

        _ts = new(
            _loopTime,
            NullLogger<LazarusService<TestService>>.Instance,
            _tp,
            _innerService,
            watchdogScopeFactory);
        _ctx = TestContext.Current?.Execution.CancellationToken?? CancellationToken.None;
    }

    [Test]
    public async Task RunsInnerLoop()
    {
        await _ts.StartAsync(_ctx);
        for (int i = 0; i < 10; i++)
        {
            await AdvanceTime();
            await Assert.That(_innerService.Counter).IsEqualTo(i);
        }
    }

    [Test]
    public async Task DoesntCatchFireIfInnerLoopThrows()
    {
        await _ts.StartAsync(_ctx);
        _innerService.CatchFire();
        await AdvanceTime();

        await Assert.That(_ts.ExecuteTask!).IsNotFaulted();
    }

    [Test]
    public async Task RespectsCancellationToken()
    {
        TimeSpan cancelAfter = TimeSpan.FromMilliseconds(100);
        CancellationTokenSource cts = new(cancelAfter, _tp);
        Task serviceTask = _ts.StartAsync(cts.Token);

        _tp.Advance(cancelAfter);
        await Assert.That(serviceTask).IsCompleted();
    }

    [Test]
    public async Task ContinuesLoopingAfterException()
    {
        _innerService.CatchFire();
        await _ts.StartAsync(_ctx);
        await AdvanceTime(); // First loop throws after delay

        await Assert.That(_innerService.Counter).IsEqualTo(0);

        _innerService.StopCatchingFire();
        await AdvanceTime(); // Waits for delay, then succeeds
        await Assert.That(_innerService.Counter).IsEqualTo(1);

        await AdvanceTime();
        await Assert.That(_innerService.Counter).IsEqualTo(2);
    }

    [Test]
    public async Task DoesNotRunLoopBeforeDelayElapsed()
    {
        await _ts.StartAsync(_ctx);

        _tp.Advance(_loopTime - TimeSpan.FromMilliseconds(1));
        await Task.Delay(100, _ctx);

        await Assert.That(_innerService.Counter).IsEqualTo(0); // No loop has run yet
    }

    [Test]
    public async Task StopAsyncStopsService()
    {
        await _ts.StartAsync(_ctx);
        await AdvanceTime();

        await _ts.StopAsync(_ctx);

        int counterAfterStop = _innerService.Counter;
        _tp.Advance(_loopTime * 5);
        await Task.Delay(100, _ctx); // Give it a chance to (incorrectly) run

        await Assert.That(_innerService.Counter).IsEqualTo(counterAfterStop);
    }

    [Test]
    public async Task RegistersHeartbeatOnEachLoop()
    {
        await _ts.StartAsync(_ctx);

        // Need two of these to guarantee one completed execution as the first advance is the initial delay
        await AdvanceTime();
        await AdvanceTime();
        Heartbeat? firstHeartbeat = _watchdog.GetLastHeartbeat();
        await AdvanceTime();
        Heartbeat? secondHeartbeat = _watchdog.GetLastHeartbeat();


        await Assert.That(firstHeartbeat).IsNotNull();
        await Assert.That(secondHeartbeat!.StartTime).IsNotEqualTo(firstHeartbeat!.StartTime);
    }

    private class TestService : IResilientService
    {
        private readonly SemaphoreSlim _loopSignal = new(1);
        private bool _shouldThrow;

        public int Counter { get; private set; }
        public string Name => "TestService";

        public Task PerformLoop(CancellationToken cancellationToken)
        {
            if (_shouldThrow)
            {
                _loopSignal.Release();
                throw new DeliberateException("I am catching fire!");
            }

            Counter++;
            _loopSignal.Release();
            return Task.CompletedTask;
        }

        public async Task WaitForLoopAsync(CancellationToken ctx)
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ctx);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            await _loopSignal.WaitAsync(cts.Token);
        }

        public void CatchFire() => _shouldThrow = true;

        public void StopCatchingFire() => _shouldThrow = false;

        public async ValueTask DisposeAsync()
        {
            _loopSignal.Dispose();
            await Task.CompletedTask;
        }
    }

    private class DeliberateException(string message) : Exception(message);

    private async Task AdvanceTime()
    {
        _tp.Advance(_loopTime);
        await Task.Delay(100, _ctx); // Give thread pool time to schedule continuation
        await _innerService.WaitForLoopAsync(_ctx);
    }

    public async ValueTask DisposeAsync()
    {
        await _ts.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
