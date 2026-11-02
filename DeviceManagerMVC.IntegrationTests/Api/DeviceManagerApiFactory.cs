using System.Net.Http.Headers;
using System.Net.Http.Json;
using DeviceManagerMVC.Api.Contracts;
using DeviceManagerMVC.Application.Dtos;
using DeviceManagerMVC.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using InfrastructureDependencyInjection = DeviceManagerMVC.Infrastructure.DependencyInjection;

namespace DeviceManagerMVC.IntegrationTests.Api;

/// <summary>
/// Boots the real API in memory, pointed at the Testcontainers database.
/// </summary>
public sealed class DeviceManagerApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "Passw0rd!";

    /// <summary>Same settings the API is given, so tests can forge tokens (expired, wrong key).</summary>
    public static readonly JwtOptions JwtOptions = new()
    {
        Issuer = "device-manager-tests",
        Audience = "device-manager-tests-spa",
        SigningKey = "integration-tests-signing-key-with-32-bytes-or-more"
    };

    private readonly string _connectionString;

    public DeviceManagerApiFactory(string connectionString)
    {
        _connectionString = connectionString;

        // HTTPS so the Secure refresh cookie is realistic; cookies handled by hand so tests
        // can assert exactly what the server sets and choose exactly what is sent back.
        ClientOptions.BaseAddress = new Uri("https://localhost");
        ClientOptions.HandleCookies = false;
    }

    /// <summary>Optional service overrides, applied after the app's own registrations.</summary>
    public Action<IServiceCollection>? ConfigureServices { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development": no developer exception page, so error responses look like production.
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            $"ConnectionStrings:{InfrastructureDependencyInjection.ConnectionStringName}", _connectionString);
        builder.UseSetting($"{JwtOptions.SectionName}:{nameof(JwtOptions.Issuer)}", JwtOptions.Issuer);
        builder.UseSetting($"{JwtOptions.SectionName}:{nameof(JwtOptions.Audience)}", JwtOptions.Audience);
        builder.UseSetting($"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)}", JwtOptions.SigningKey);

        if (ConfigureServices is not null)
        {
            builder.ConfigureTestServices(ConfigureServices);
        }
    }

    /// <summary>Registers a fresh user through the API, logs in and returns a client carrying the bearer token.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";

        (await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, Password))).EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<AccessTokenResponse>())!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }
}
