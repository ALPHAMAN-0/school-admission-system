using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SchoolAdmission.Api.Tests.Infrastructure;

/// <summary>Hosts the real API in memory against a real PostgreSQL database.</summary>
public sealed class AdmissionApiFactory(string connectionString, TimeProvider? timeProvider = null, int holdDurationSeconds = 600)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connectionString,
            ["HOLD_DURATION_SECONDS"] = holdDurationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            // Keep the background sweeper out of the way; tests call it explicitly when needed.
            ["HOLD_SWEEP_INTERVAL_SECONDS"] = "3600",
        }));

        if (timeProvider is not null)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(timeProvider);
            });
        }
    }
}
