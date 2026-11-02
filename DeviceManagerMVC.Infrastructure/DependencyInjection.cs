using DeviceManagerMVC.Domain.Repositories;
using DeviceManagerMVC.Infrastructure.Persistence;
using DeviceManagerMVC.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeviceManagerMVC.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "DeviceManager";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Fail at startup, not on the first request.
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddSingleton(new SqlConnectionFactory(connectionString));
        services.AddScoped<IDeviceRepository, DeviceRepository>();

        return services;
    }
}
