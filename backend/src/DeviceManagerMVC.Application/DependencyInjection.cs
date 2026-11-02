using DeviceManagerMVC.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DeviceManagerMVC.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // TryAdd so tests can register a FakeTimeProvider first.
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<DeviceService>();
        services.AddScoped<AuthService>();

        return services;
    }
}
