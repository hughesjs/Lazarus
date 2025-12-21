using System.Reflection;
using System.Text.Json;
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
        await _serviceTwo.WaitForLoopAsync(_ctx);
        HttpResponseMessage res = await _client.GetAsync("/health", _ctx);
        await Assert.That(res).IsSuccessStatusCode();
    }


    [Test]
    public async Task MultipleServicesIndependentResults()
    {
        _timeProvider.Advance((LazarusTestWebApplicationFactory.IntervalOne + LazarusTestWebApplicationFactory.IntervalTwo) / 2);

        await _serviceOne.WaitForLoopAsync(_ctx);
        await _serviceTwo.WaitForLoopAsync(_ctx);

        HttpResponseMessage res = await _client.GetAsync("/health", _ctx);

        string content = await res.Content.ReadAsStringAsync(_ctx);

        using JsonDocument doc = JsonDocument.Parse(content);
        JsonElement entries = doc.RootElement.GetProperty("entries");

        // Get all TestService entries - use service type name from metadata to identify them
        List<JsonProperty> testServiceEntries = entries.EnumerateObject()
            .Where(e => e.Value.TryGetProperty("data", out JsonElement data) &&
                       data.TryGetProperty("service", out JsonElement service) &&
                       service.GetString() != null &&
                       service.GetString()!.Contains("TestService"))
            .ToList();

        await Assert.That(testServiceEntries).Count().IsEqualTo(2);

        using (Assert.Multiple())
        {
            foreach (JsonProperty entry in testServiceEntries)
            {
                JsonElement data = entry.Value.GetProperty("data");
                bool hasHeartbeat = data.TryGetProperty("lastHeartbeat", out JsonElement _);
                await Assert.That(hasHeartbeat).IsTrue();
            }
        }
    }

    [Test]
    public async Task DifferentConfigurationsResultInDifferentHealthStatus()
    {
        // ServiceOne (string) has IntervalOne = 5s, unhealthy timeout = 10s, degraded timeout = 7.5s
        // ServiceTwo (object) has IntervalTwo = 7s, unhealthy timeout = 14s, degraded timeout = 10.5s

        // Clear existing heartbeats and set specific ones to test configuration differences
#pragma warning disable CA2201
        IWatchdogService<TestService<string>> watchdogString = _factory.Services.GetRequiredService<IWatchdogService<TestService<string>>>();
        IWatchdogService<TestService<object>> watchdogObject = _factory.Services.GetRequiredService<IWatchdogService<TestService<object>>>();

        FieldInfo recentHeartbeatsFieldString = watchdogString.GetType().GetField("_recentHeartbeats", BindingFlags.NonPublic | BindingFlags.Instance) ?? throw new("Did you change the internal field of the watchdog?");
        FieldInfo recentHeartbeatsFieldObject = watchdogObject.GetType().GetField("_recentHeartbeats", BindingFlags.NonPublic | BindingFlags.Instance) ?? throw new("Did you change the internal field of the watchdog?");

        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTimeOffset elevenSecondsAgo = now - TimeSpan.FromSeconds(11);

        // Set both services to have a heartbeat from 11 seconds ago
        List<Heartbeat> stringHeartbeats = new()
        {
            new() { StartTime = elevenSecondsAgo, EndTime = elevenSecondsAgo, Exception = null }
        };
        List<Heartbeat> objectHeartbeats = new()
        {
            new() { StartTime = elevenSecondsAgo, EndTime = elevenSecondsAgo, Exception = null }
        };

        recentHeartbeatsFieldString.SetValue(watchdogString, stringHeartbeats);
        recentHeartbeatsFieldObject.SetValue(watchdogObject, objectHeartbeats);
#pragma warning restore CA2201

        HttpResponseMessage res = await _client.GetAsync("/health", _ctx);
        string content = await res.Content.ReadAsStringAsync(_ctx);

        using JsonDocument doc = JsonDocument.Parse(content);
        JsonElement entries = doc.RootElement.GetProperty("entries");

        // Use configuration metadata to identify which service is which
        // ServiceString has unhealthy timeout of 10s
        // ServiceObject has unhealthy timeout of 14s
        JsonProperty? stringServiceEntry = null;
        JsonProperty? objectServiceEntry = null;

        foreach (JsonProperty entry in entries.EnumerateObject())
        {
            if (entry.Value.TryGetProperty("data", out JsonElement data) &&
                data.TryGetProperty("configuration", out JsonElement config) &&
                config.TryGetProperty("unhealthyTimeSinceLastHeartbeat", out JsonElement unhealthyTimeout))
            {
                string? timeoutStr = unhealthyTimeout.GetString();
                if (timeoutStr != null)
                {
                    if (timeoutStr.Contains("00:00:10"))
                    {
                        stringServiceEntry = entry;
                    }
                    else if (timeoutStr.Contains("00:00:14"))
                    {
                        objectServiceEntry = entry;
                    }
                }
            }
        }

        await Assert.That(stringServiceEntry.HasValue).IsTrue();
        await Assert.That(objectServiceEntry.HasValue).IsTrue();

        JsonElement serviceStringStatus = stringServiceEntry!.Value.Value.GetProperty("status");
        JsonElement serviceObjectStatus = objectServiceEntry!.Value.Value.GetProperty("status");

        using (Assert.Multiple())
        {
            // ServiceString: 11s since last heartbeat, unhealthy threshold is 10s -> Unhealthy
            await Assert.That(serviceStringStatus.GetString()).IsEqualTo("Unhealthy");
            // ServiceObject: 11s since last heartbeat, degraded threshold is 10.5s, unhealthy is 14s -> Degraded
            await Assert.That(serviceObjectStatus.GetString()).IsEqualTo("Degraded");
        }
    }


    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
