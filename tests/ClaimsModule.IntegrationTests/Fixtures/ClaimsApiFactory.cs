using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.Fixtures;

/// <summary>
/// Hosts the real API (Program.cs) in memory, in the Development environment, against the test
/// database. Adds <see cref="ProbeController"/> so the cross-cutting pipeline can be exercised
/// without production test endpoints.
/// </summary>
public sealed class ClaimsApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:ClaimsDb", connectionString);

        builder.ConfigureTestServices(services =>
            services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly));
    }
}
