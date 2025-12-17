using System.Collections.Concurrent;
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
    private readonly IWatchdogService _watchdog;
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

        _watchdog = _factory.Services.GetRequiredService<IWatchdogService>();
        _serviceOne = _factory.Services.GetRequiredService<TestService<object>>();
        _serviceTwo = _factory.Services.GetRequiredService<TestService<string>>();

        _client = _factory.CreateClient();

        _ctx = TestContext.Current?.Execution.CancellationToken ?? CancellationToken.None;
    }

    [Test]
    public async Task NoHeartbeat_ReturnsUnhealthyResponse()
    {
        // Kill the first-loop heartbeat with reflection to avoid needing to expose the internals
        // or change the implementation just to support this one tests
#pragma warning disable CA2201
        FieldInfo lastHeartbeatsField = _watchdog.GetType().GetField("_lastHeartbeats", BindingFlags.NonPublic | BindingFlags.Instance) ?? throw new("Did you change the internal field of the watchdog?");
        ConcurrentDictionary<Type, DateTimeOffset> lastHeartbeats = (ConcurrentDictionary<Type, DateTimeOffset>)lastHeartbeatsField.GetValue(_watchdog)!;
        MethodInfo clear = lastHeartbeats.GetType().GetMethod("Clear") ?? throw new("Did you change the internal field of the watchdog?");
        clear.Invoke(lastHeartbeats, []);
#pragma warning restore CA2201

        HttpResponseMessage res = await _client.GetAsync("/health", _ctx);
        await Assert.That(res).IsNotSuccessStatusCode();
    }

    [Test]
    public async Task ServiceRunning_ReturnsHealthyResponse()
    {
        HttpResponseMessage res = await _client.GetAsync("/health", _ctx);
        await Assert.That(res).IsSuccessStatusCode();
    }


    [Test]
    public async Task MultipleServices_IndependentResults()
    {
        _timeProvider.Advance((LazarusTestWebApplicationFactory.IntervalOne + LazarusTestWebApplicationFactory.IntervalTwo) / 2);

        await _serviceOne.PerformLoop(_ctx); // This should have a new heartbeat
        await _serviceTwo.PerformLoop(_ctx); // This one shouldn't

        HttpResponseMessage res = await _client.GetAsync("/health", _ctx);

        string content = await res.Content.ReadAsStringAsync(_ctx);

        using JsonDocument doc = JsonDocument.Parse(content);
        JsonElement entries = doc.RootElement.GetProperty("entries");

        DateTimeOffset? heartbeat1 = null;
        DateTimeOffset? heartbeat2 = null;

        foreach (DateTimeOffset lastHeartbeat in entries.EnumerateObject()
                     .Where(entry => entry.Name.Contains("TestService"))
                     .Select(entry => entry.Value.GetProperty("data"))
                     .Select(data => DateTimeOffset.Parse(data.GetProperty("lastHeartbeat").GetString()!)))
        {
            if (heartbeat1 is null)
            {
                heartbeat1 = lastHeartbeat;
            }
            else
            {
                heartbeat2 = lastHeartbeat;
            }
        }

        await Assert.That(heartbeat1).IsNotNull();
        await Assert.That(heartbeat2).IsNotNull();
        await Assert.That(heartbeat1).IsNotEqualTo(heartbeat2);
    }


    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
