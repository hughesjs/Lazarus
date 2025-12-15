using Lazarus.Internal.Service;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Lazarus.Tests.Unit;

public class LazarusServiceTests : IDisposable
{
    private readonly TestService _ts;
    private readonly FakeTimeProvider _tp;
    private readonly CancellationToken _ctx;

    private readonly TimeSpan _loopTime = TimeSpan.FromSeconds(5);

    public LazarusServiceTests()
    {
        _tp = new();
        _ts = new(_loopTime, NullLogger<TestService>.Instance, _tp);
        _ctx = TestContext.Current?.Execution.CancellationToken?? CancellationToken.None;
    }

    [Test]
    public async Task RunsInnerLoop()
    {
        await _ts.StartAsync(_ctx);
        for (int i = 0; i < 10; i++)
        {
            _tp.Advance(_loopTime);
            await _ts.WaitForLoopAsync();
            await Assert.That(_ts.Counter).IsEqualTo(i + 1);
        }
    }

    [Test]
    public async Task DoesntCatchFireIfInnerLoopThrows()
    {
        await _ts.StartAsync(_ctx);
        _ts.CatchFire();
        _tp.Advance(_loopTime);
        await _ts.WaitForLoopAsync();

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
        _ = _ts.StartAsync(CancellationToken.None); // Runs one loop immediately
        _tp.Advance(_loopTime);
        await _ts.WaitForLoopAsync(); // Run the loop again

        await Assert.That(_ts.Counter).IsEqualTo(0);

        _ts.StopCatchingFire();
        _tp.Advance(_loopTime);
        await _ts.WaitForLoopAsync();
        await Assert.That(_ts.Counter).IsEqualTo(1);

        _ts.StopCatchingFire();
        _tp.Advance(_loopTime);
        await _ts.WaitForLoopAsync();
        await Assert.That(_ts.Counter).IsEqualTo(2);
    }

    [Test]
    public async Task DoesNotRunSecondLoopBeforeDelayElapsed()
    {
        _ = _ts.StartAsync(CancellationToken.None);
        await _ts.WaitForLoopAsync(); // First loop runs immediately

        _tp.Advance(_loopTime - TimeSpan.FromMilliseconds(1));
        await Task.Delay(10, _ctx);

        await Assert.That(_ts.Counter).IsEqualTo(1); // Still just 1, second loop hasn't run
    }

    [Test]
    public async Task StopAsyncStopsService()
    {
        _ = _ts.StartAsync(CancellationToken.None);
        _tp.Advance(_loopTime);
        await _ts.WaitForLoopAsync();

        await _ts.StopAsync(CancellationToken.None);

        int counterAfterStop = _ts.Counter;
        _tp.Advance(_loopTime * 5);
        await Task.Delay(10); // Give it a chance to (incorrectly) run

        await Assert.That(_ts.Counter).IsEqualTo(counterAfterStop);
    }

    private class TestService : LazarusService
    {
        private readonly SemaphoreSlim _loopSignal;
        private bool _shouldThrow;

        public int Counter { get; private set; }

        public TestService(TimeSpan loopDelay, ILogger<LazarusService> logger, TimeProvider timeProvider) : base(loopDelay, logger, timeProvider) => _loopSignal = new(0);

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

        public async Task WaitForLoopAsync() => await _loopSignal.WaitAsync();

        public void CatchFire() => _shouldThrow = true;

        public void StopCatchingFire() => _shouldThrow = false;
    }

    private class DeliberateException(string message) : Exception(message);

    public void Dispose()
    {
        _ts.Dispose();
        GC.SuppressFinalize(this);
    }
}
