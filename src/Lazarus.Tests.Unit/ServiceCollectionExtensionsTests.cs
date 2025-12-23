using Lazarus.Public;
using Lazarus.Public.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lazarus.Tests.Unit;

public class ServiceCollectionExtensionsTests
{
    [Test]
    public async Task RegistersAndExecutesOneLoop()
    {
        TimeSpan shortDelay = TimeSpan.FromMilliseconds(50);
        ServiceCollection services = new();

        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddLazarusService<TestService>(_ => shortDelay, _ => TimeSpan.FromMinutes(5));

        await using ServiceProvider provider = services.BuildServiceProvider();

        IEnumerable<IHostedService> hostedServices = provider.GetServices<IHostedService>();
        IHostedService? hostedService = hostedServices.FirstOrDefault();

        await Assert.That(hostedService).IsNotNull();

        TestService? innerService = provider.GetService<TestService>();
        await Assert.That(innerService).IsNotNull();

        using CancellationTokenSource cts = new();
        await hostedService!.StartAsync(cts.Token);

        try
        {
            await innerService!.WaitForLoopAsync(cts.Token);
            await Assert.That(innerService.Counter).IsEqualTo(1);
        }
        finally
        {
            await hostedService.StopAsync(cts.Token);
        }
    }

    [Test]
    public async Task ThrowsOnDuplicateRegistration()
    {
        ServiceCollection services = new();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddLazarusService<TestService>(_ => TimeSpan.FromSeconds(1), _ => TimeSpan.FromMinutes(5));

        await Assert.That(() => services.AddLazarusService<TestService>(_ => TimeSpan.FromSeconds(1), _=> TimeSpan.FromMinutes(5)))
            .Throws<LazarusConfigurationException>();
    }

    private class TestService : IResilientService
    {
        private readonly SemaphoreSlim _loopSignal = new(0);

        public int Counter { get; private set; }
        public string Name => "TestService";

        public Task PerformLoop(CancellationToken cancellationToken)
        {
            Counter++;
            _loopSignal.Release();
            return Task.CompletedTask;
        }

        public async Task WaitForLoopAsync(CancellationToken ct)
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            await _loopSignal.WaitAsync(cts.Token);
        }

        public async ValueTask DisposeAsync()
        {
            _loopSignal.Dispose();
            await Task.CompletedTask;
        }
    }
}
