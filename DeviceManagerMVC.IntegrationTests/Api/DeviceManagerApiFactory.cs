using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using InfrastructureDependencyInjection = DeviceManagerMVC.Infrastructure.DependencyInjection;

namespace DeviceManagerMVC.IntegrationTests.Api;

/// <summary>
/// Boots the real API in memory, pointed at the Testcontainers database.
/// </summary>
public sealed class DeviceManagerApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    /// <summary>Optional service overrides, applied after the app's own registrations.</summary>
    public Action<IServiceCollection>? ConfigureServices { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development": no developer exception page, so error responses look like production.
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            $"ConnectionStrings:{InfrastructureDependencyInjection.ConnectionStringName}", connectionString);

        if (ConfigureServices is not null)
        {
            builder.ConfigureTestServices(ConfigureServices);
        }
    }
}
