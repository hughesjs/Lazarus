using System.Reflection;
using System.Text.Json;
using Lazarus.Extensions.HealthChecks.Tests.Integration.App;
using Lazarus.Extensions.HealthChecks.Tests.Integration.Fixture;
using Lazarus.Public.Watchdog;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Lazarus.Extensions.HealthChecks.Tests.Integration;

public class HealthCheckIntegrationTests : IAsyncDisposable
{
    private readonly WebApplicationFactory<LazarusTestWebApplicationFactory> _factory;
    private readonly HttpClient _client;
    private readonly FakeTimeProvider _timeProvider;
    private readonly TestService<object> _serviceOne;
    private readonly TestService<string> _serviceTwo;
    private readonly CancellationToken _ctx;

    public HealthCheckIntegrationTests()
    {
        _timeProvider = new();

        _factory = new LazarusTestWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddRouting();
                    services.Replace(new(typeof(TimeProvider), _timeProvider));
                });
            });

        _serviceOne = _factory.Services.GetRequiredService<TestService<object>>();
        _serviceTwo = _factory.Services.GetRequiredService<TestService<string>>();

        _client = _factory.CreateClient();

        _ctx = TestContext.Current?.Execution.CancellationToken ?? CancellationToken.None;
    }

    [Test]
    public async Task NoHeartbeatReturnsUnhealthyResponse()
    {
        // Clear the heartbeat list with reflection to avoid needing to expose the internals
        // or change the implementation just to support this one test
#pragma warning disable CA2201
        IWatchdogService<TestService<object>> watchdogOne = _factory.Services.GetRequiredService<IWatchdogService<TestService<object>>>();
        IWatchdogService<TestService<string>> watchdogTwo = _factory.Services.GetRequiredService<IWatchdogService<TestService<string>>>();

        FieldInfo recentHeartbeatsFieldOne = watchdogOne.GetType().GetField("_recentHeartbeats", BindingFlags.NonPublic | BindingFlags.Instance) ?? throw new("Did you change the internal field of the watchdog?");
        FieldInfo recentHeartbeatsFieldTwo = watchdogTwo.GetType().GetField("_recentHeartbeats", BindingFlags.NonPublic | BindingFlags.Instance) ?? throw new("Did you change the internal field of the watchdog?");

        recentHeartbeatsFieldOne.SetValue(watchdogOne, new List<Heartbeat>());
        recentHeartbeatsFieldTwo.SetValue(watchdogTwo, new List<Heartbeat>());
#pragma warning restore CA2201

        HttpResponseMessage res = await _client.GetAsync("/health", _ctx);
        await Assert.That(res).IsNotSuccessStatusCode();
    }

    [Test]
    public async Task ServiceRunningReturnsHealthyResponse()
    {
        _timeProvider.Advance(LazarusTestWebApplicationFactory.IntervalTwo);
        await _serviceTwo.PerformLoop(_ctx);
        HttpResponseMessage res = await _client.GetAsync("/health", _ctx);
        await Assert.That(res).IsSuccessStatusCode();
    }


    [Test]
    public async Task MultipleServicesIndependentResults()
    {
        _timeProvider.Advance((LazarusTestWebApplicationFactory.IntervalOne + LazarusTestWebApplicationFactory.IntervalTwo) / 2);

        await _serviceOne.PerformLoop(_ctx);
        await _serviceTwo.PerformLoop(_ctx);

        HttpResponseMessage res = await _client.GetAsync("/health", _ctx);

        string content = await res.Content.ReadAsStringAsync(_ctx);

        using JsonDocument doc = JsonDocument.Parse(content);
        JsonElement entries = doc.RootElement.GetProperty("entries");

        // Technically order isn't gauranteed here but in practice I've never seen it fail.
        // If this test is flakey, start here
        List<JsonProperty> testServiceEntries = entries.EnumerateObject()
            .Where(e => e.Name.Contains("TestService"))
            .ToList();

        JsonElement serviceOneCheckData = testServiceEntries[0].Value.GetProperty("data");
        JsonElement serviceTwoCheckData = testServiceEntries[1].Value.GetProperty("data");

        bool firstServiceHasHeartbeat = serviceOneCheckData.TryGetProperty("lastHeartbeat", out JsonElement _);
        bool secondServiceHasHeartbeat = serviceTwoCheckData.TryGetProperty("lastHeartbeat", out JsonElement _);

        using (Assert.Multiple())
        {
            await Assert.That(firstServiceHasHeartbeat).IsTrue();
            await Assert.That(secondServiceHasHeartbeat).IsTrue();
        }
    }


    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
