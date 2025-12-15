using Lazarus.Internal.Service;
using Lazarus.Internal.Watchdog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Lazarus.Tests.Unit;

public class LazarusServiceTests : IDisposable
{
    private readonly TestService _ts;
    private readonly FakeTimeProvider _tp;
    private readonly IWatchdogService<LazarusService> _watchdog;
    private readonly CancellationToken _ctx;

    private readonly TimeSpan _loopTime = TimeSpan.FromSeconds(5);

    public LazarusServiceTests()
    {
        _tp = new();
        _watchdog = new InMemoryWatchdogService<LazarusService>(_tp);
        _ts = new(_loopTime, NullLogger<TestService>.Instance, _tp, _watchdog);
        _ctx = TestContext.Current?.Execution.CancellationToken?? CancellationToken.None;
    }

    [Test]
    public async Task RunsInnerLoop()
    {
        await _ts.StartAsync(_ctx);
        for (int i = 0; i < 10; i++)
        {
            await AdvanceTime();
            await Assert.That(_ts.Counter).IsEqualTo(i);
        }
    }

    [Test]
    public async Task DoesntCatchFireIfInnerLoopThrows()
    {
        await _ts.StartAsync(_ctx);
        _ts.CatchFire();
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
        _ts.CatchFire();
        await _ts.StartAsync(_ctx);
        await AdvanceTime(); // First loop throws after delay

        await Assert.That(_ts.Counter).IsEqualTo(0);

        _ts.StopCatchingFire();
        await AdvanceTime(); // Waits for delay, then succeeds
        await Assert.That(_ts.Counter).IsEqualTo(1);

        await AdvanceTime();
        await Assert.That(_ts.Counter).IsEqualTo(2);
    }

    [Test]
    public async Task DoesNotRunLoopBeforeDelayElapsed()
    {
        await _ts.StartAsync(_ctx);

        _tp.Advance(_loopTime - TimeSpan.FromMilliseconds(1));
        await Task.Delay(100, _ctx);

        await Assert.That(_ts.Counter).IsEqualTo(0); // No loop has run yet
    }

    [Test]
    public async Task StopAsyncStopsService()
    {
        await _ts.StartAsync(_ctx);
        await AdvanceTime();

        await _ts.StopAsync(_ctx);

        int counterAfterStop = _ts.Counter;
        _tp.Advance(_loopTime * 5);
        await Task.Delay(100, _ctx); // Give it a chance to (incorrectly) run

        await Assert.That(_ts.Counter).IsEqualTo(counterAfterStop);
    }

    [Test]
    public async Task RegistersHeartbeatOnEachLoop()
    {
        await _ts.StartAsync(_ctx);

        await AdvanceTime();
        DateTimeOffset? firstHeartbeat = _watchdog.GetLastHeartbeat(_ts);
        await Assert.That(firstHeartbeat).IsNotNull();

        await AdvanceTime();
        DateTimeOffset? secondHeartbeat = _watchdog.GetLastHeartbeat(_ts);
        await Assert.That(secondHeartbeat!).IsNotEqualTo(firstHeartbeat);
    }

    private class TestService : LazarusService
    {
        private readonly SemaphoreSlim _loopSignal;
        private bool _shouldThrow;

        public int Counter { get; private set; }

        public TestService(TimeSpan loopDelay, ILogger<LazarusService> logger, TimeProvider timeProvider, IWatchdogService<LazarusService> watchdog) : base(loopDelay, logger, timeProvider, watchdog) => _loopSignal = new(1);

        protected override Task PerformLoop(CancellationToken cancellationToken)
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

        public override void Dispose() => _loopSignal.Dispose();
    }

    private class DeliberateException(string message) : Exception(message);

    private async Task AdvanceTime()
    {
        _tp.Advance(_loopTime);
        await Task.Delay(100); // Give thread pool time to schedule continuation
        await _ts.WaitForLoopAsync(_ctx);
    }

    public void Dispose()
    {
        _ts.Dispose();
        GC.SuppressFinalize(this);
    }
}
