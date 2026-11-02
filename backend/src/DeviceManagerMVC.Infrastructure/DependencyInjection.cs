using DeviceManagerMVC.Application.Abstractions;
using DeviceManagerMVC.Domain.Repositories;
using DeviceManagerMVC.Infrastructure.Persistence;
using DeviceManagerMVC.Infrastructure.Repositories;
using DeviceManagerMVC.Infrastructure.Security;
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
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // Registered as a singleton so the API's JWT bearer validation reads the very same settings.
        services.AddSingleton(ReadJwtOptions(configuration));
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();

        return services;
    }

    private static JwtOptions ReadJwtOptions(IConfiguration configuration)
    {
        var section = configuration.GetSection(JwtOptions.SectionName);

        return new JwtOptions
        {
            Issuer = Required(section, nameof(JwtOptions.Issuer)),
            Audience = Required(section, nameof(JwtOptions.Audience)),
            SigningKey = Required(section, nameof(JwtOptions.SigningKey))
        };
    }

    private static string Required(IConfigurationSection section, string key) =>
        section[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Configuration value '{section.Path}:{key}' is not configured.");
}
