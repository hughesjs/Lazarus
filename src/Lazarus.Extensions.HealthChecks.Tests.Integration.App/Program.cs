using HealthChecks.UI.Client;
using Lazarus.Extensions.HealthChecks.Public;
using Lazarus.Extensions.HealthChecks.Tests.Integration.App;
using Lazarus.Public.Configuration;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;


WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddLazarusService<TestService<string>>(INTERVAL_ONE);
builder.Services.AddLazarusService<TestService<object>>(INTERVAL_TWO);

builder.Services.AddHealthChecks().AddLazarusHealthcheck<TestService<string>>(INTERVAL_ONE * 2);
builder.Services.AddHealthChecks().AddLazarusHealthcheck<TestService<object>>(INTERVAL_TWO * 2);

WebApplication app = builder.Build();

app.MapHealthChecks("/health", new HealthCheckOptions()
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
app.UseHttpsRedirection();

app.Run();


public partial class Program
{
    public static readonly TimeSpan INTERVAL_ONE = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan INTERVAL_TWO = TimeSpan.FromSeconds(7);
}
