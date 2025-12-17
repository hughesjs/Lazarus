using Lazarus.Public;

namespace Lazarus.Extensions.HealthChecks.Tests.Integration.App;

/// <summary>
///
/// </summary>
/// <typeparam name="TDifferentialKey">Just a key to let us pretend these are different classes when registering two implementations</typeparam>
public class TestService<TDifferentialKey> : IResilientService
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

public class DeliberateException(string message) : Exception(message);
