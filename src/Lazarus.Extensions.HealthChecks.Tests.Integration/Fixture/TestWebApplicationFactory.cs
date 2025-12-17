using HealthChecks.UI.Client;
using Lazarus.Extensions.HealthChecks.Public;
using Lazarus.Extensions.HealthChecks.Tests.Integration.App;
using Lazarus.Public.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

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
                    .ConfigureServices(services =>
                    {
                        services.AddLazarusService<TestService<string>>(IntervalOne);
                        services.AddLazarusService<TestService<object>>(IntervalTwo);
                        services.AddHealthChecks()
                            .AddLazarusHealthcheck<TestService<string>>(IntervalOne * 2)
                            .AddLazarusHealthcheck<TestService<object>>(IntervalTwo * 2);
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
