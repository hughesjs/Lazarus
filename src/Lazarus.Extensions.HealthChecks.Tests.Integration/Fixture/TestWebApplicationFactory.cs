using HealthChecks.UI.Client;
using Lazarus.Extensions.HealthChecks.Public;
using Lazarus.Extensions.HealthChecks.Tests.Integration.App;
using Lazarus.Public.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;

namespace Lazarus.Extensions.HealthChecks.Tests.Integration.Fixture;

public class LazarusTestWebApplicationFactory : WebApplicationFactory<LazarusTestWebApplicationFactory>
{
    public static readonly TimeSpan IntervalOne = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan IntervalTwo = TimeSpan.FromSeconds(7);

    protected override IHostBuilder CreateHostBuilder() =>
        new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .UseContentRoot(Directory.GetCurrentDirectory())
                    .ConfigureServices(services =>
                    {
                        Dictionary<string, string?> configDict = new()
                        {
                            ["HealthChecks:TestServiceString:UnhealthyTimeSinceLastHeartbeat"] = (IntervalOne * 2).ToString(),
                            ["HealthChecks:TestServiceString:DegradedTimeSinceLastHeartbeat"] = (IntervalOne * 1.5).ToString(),
                            ["HealthChecks:TestServiceString:UnhealthyExceptionCountThreshold"] = "5",
                            ["HealthChecks:TestServiceString:DegradedExceptionCountThreshold"] = "2",
                            ["HealthChecks:TestServiceString:ExceptionCounterSlidingWindow"] = "00:05:00",

                            ["HealthChecks:TestServiceObject:UnhealthyTimeSinceLastHeartbeat"] = (IntervalTwo * 2).ToString(),
                            ["HealthChecks:TestServiceObject:DegradedTimeSinceLastHeartbeat"] = (IntervalTwo * 1.5).ToString(),
                            ["HealthChecks:TestServiceObject:UnhealthyExceptionCountThreshold"] = "5",
                            ["HealthChecks:TestServiceObject:DegradedExceptionCountThreshold"] = "2",
                            ["HealthChecks:TestServiceObject:ExceptionCounterSlidingWindow"] = "00:05:00"
                        };

                        IConfiguration configuration = new ConfigurationBuilder()
                            .AddInMemoryCollection(configDict)
                            .Build();

                        services.AddLazarusService<TestService<string>>(_ => IntervalOne, exceptionWindow: _ => TimeSpan.FromMinutes(5));
                        services.AddLazarusService<TestService<object>>(_ => IntervalTwo, exceptionWindow: _ => TimeSpan.FromMinutes(5));
                        services.AddHealthChecks()
                            .AddLazarusHealthCheck<TestService<string>>(
                                configuration.GetSection("HealthChecks:TestServiceString"))
                            .AddLazarusHealthCheck<TestService<object>>(
                                configuration.GetSection("HealthChecks:TestServiceObject"));
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapHealthChecks("/health", new()
                            {
                                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
                            });
                        });
                        // Note: UseHttpsRedirection typically not needed in tests
                    });
            });
}
