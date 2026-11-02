using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DeviceManagerMVC.Api.Contracts;
using DeviceManagerMVC.Api.Controllers;
using DeviceManagerMVC.Application.Dtos;
using DeviceManagerMVC.IntegrationTests.Infrastructure.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Shouldly;
using SameSiteMode = Microsoft.Net.Http.Headers.SameSiteMode;

namespace DeviceManagerMVC.IntegrationTests.Api;

[Collection(DatabaseCollection.Name)]
public sealed class AuthControllerTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string Email = "user@example.com";
    private const string Password = DeviceManagerApiFactory.Password;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly DeviceManagerApiFactory _factory = new(fixture.ConnectionString);
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetUsersAsync();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private Task<HttpResponseMessage> RegisterAsync(string email = Email, string password = Password) =>
        _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password), Json);

    private Task<HttpResponseMessage> LoginAsync(string email = Email, string password = Password) =>
        _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), Json);

    /// <summary>Registers and logs in; returns the raw refresh token from the cookie.</summary>
    private async Task<string> GivenLoggedInAsync()
    {
        (await RegisterAsync()).EnsureSuccessStatusCode();
        var login = await LoginAsync();
        login.EnsureSuccessStatusCode();
        return RefreshCookieFrom(login).Value.ToString();
    }

    private async Task<HttpResponseMessage> PostWithRefreshCookieAsync(string action, string? refreshToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/auth/{action}");
        if (refreshToken is not null)
        {
            request.Headers.Add(HeaderNames.Cookie, $"{AuthController.RefreshTokenCookieName}={refreshToken}");
        }

        return await _client.SendAsync(request);
    }

    private static SetCookieHeaderValue RefreshCookieFrom(HttpResponseMessage response)
    {
        response.Headers.TryGetValues(HeaderNames.SetCookie, out var values).ShouldBeTrue("no Set-Cookie header");
        return SetCookieHeaderValue.ParseList(values!.ToList())
            .Single(cookie => cookie.Name == AuthController.RefreshTokenCookieName);
    }

    private static async Task ShouldBeProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json);
        problem!.Status.ShouldBe((int)status);
    }

    // ---------- Register ----------

    [Fact]
    public async Task Register_returns_201_with_the_user_and_no_password_data()
    {
        var response = await RegisterAsync(" User@Example.com ");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain("password", Case.Insensitive);
        var user = JsonSerializer.Deserialize<UserResponse>(body, Json)!;
        user.Id.ShouldBeGreaterThan(0);
        user.Email.ShouldBe(Email);
    }

    [Fact]
    public async Task Register_with_short_password_returns_400_problem()
    {
        await ShouldBeProblemAsync(await RegisterAsync(password: "1234567"), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_with_invalid_email_returns_400_problem()
    {
        await ShouldBeProblemAsync(await RegisterAsync(email: "not-an-email"), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_with_existing_email_returns_409_problem()
    {
        (await RegisterAsync()).EnsureSuccessStatusCode();

        await ShouldBeProblemAsync(await RegisterAsync("USER@example.com"), HttpStatusCode.Conflict);
    }

    // ---------- Login ----------

    [Fact]
    public async Task Login_returns_15_minute_access_token_in_body()
    {
        (await RegisterAsync()).EnsureSuccessStatusCode();

        var response = await LoginAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var token = (await response.Content.ReadFromJsonAsync<AccessTokenResponse>(Json))!;
        token.AccessToken.ShouldNotBeNullOrWhiteSpace();
        token.ExpiresAtUtc.ShouldBe(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Login_sets_refresh_token_as_HttpOnly_Secure_SameSite_Strict_cookie_scoped_to_auth()
    {
        (await RegisterAsync()).EnsureSuccessStatusCode();

        var response = await LoginAsync();

        var cookie = RefreshCookieFrom(response);
        cookie.Value.ToString().ShouldNotBeNullOrWhiteSpace();
        cookie.HttpOnly.ShouldBeTrue();
        cookie.Secure.ShouldBeTrue();
        cookie.SameSite.ShouldBe(SameSiteMode.Strict);
        cookie.Path.ToString().ShouldBe(AuthController.RefreshTokenCookiePath);
        cookie.Expires!.Value.UtcDateTime.ShouldBe(DateTime.UtcNow.AddDays(7), TimeSpan.FromMinutes(1));

        // Only the cookie carries it: script-readable JSON never does.
        (await response.Content.ReadAsStringAsync()).ShouldNotContain(cookie.Value.ToString());
    }

    [Fact]
    public async Task Login_access_token_authorizes_device_requests()
    {
        (await RegisterAsync()).EnsureSuccessStatusCode();
        var token = (await (await LoginAsync()).Content.ReadFromJsonAsync<AccessTokenResponse>(Json))!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await _client.GetAsync("/api/devices");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401_problem()
    {
        (await RegisterAsync()).EnsureSuccessStatusCode();

        await ShouldBeProblemAsync(await LoginAsync(password: "wrong-password"), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_with_unknown_email_returns_401_problem()
    {
        await ShouldBeProblemAsync(await LoginAsync(email: "nobody@example.com"), HttpStatusCode.Unauthorized);
    }

    // ---------- Refresh ----------

    [Fact]
    public async Task Refresh_returns_new_access_token_and_rotates_the_cookie()
    {
        var refreshToken = await GivenLoggedInAsync();

        var response = await PostWithRefreshCookieAsync("refresh", refreshToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<AccessTokenResponse>(Json))!.AccessToken.ShouldNotBeNullOrWhiteSpace();
        var rotated = RefreshCookieFrom(response);
        rotated.Value.ToString().ShouldNotBe(refreshToken);
        rotated.HttpOnly.ShouldBeTrue();
        rotated.Secure.ShouldBeTrue();
        rotated.SameSite.ShouldBe(SameSiteMode.Strict);
        rotated.Path.ToString().ShouldBe(AuthController.RefreshTokenCookiePath);
    }

    [Fact]
    public async Task Refresh_without_cookie_returns_401_problem()
    {
        await ShouldBeProblemAsync(await PostWithRefreshCookieAsync("refresh", null), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_with_unknown_cookie_returns_401_problem()
    {
        await ShouldBeProblemAsync(
            await PostWithRefreshCookieAsync("refresh", "not-a-real-token"), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_reusing_a_rotated_cookie_returns_401_and_revokes_the_whole_family()
    {
        var original = await GivenLoggedInAsync();
        var rotated = RefreshCookieFrom(await PostWithRefreshCookieAsync("refresh", original)).Value.ToString();

        var reuse = await PostWithRefreshCookieAsync("refresh", original);

        await ShouldBeProblemAsync(reuse, HttpStatusCode.Unauthorized);
        // The legitimate holder of the newest token is logged out too: the family is burned.
        await ShouldBeProblemAsync(await PostWithRefreshCookieAsync("refresh", rotated), HttpStatusCode.Unauthorized);
    }

    // ---------- Logout ----------

    [Fact]
    public async Task Logout_returns_204_clears_the_cookie_and_revokes_the_session()
    {
        var refreshToken = await GivenLoggedInAsync();

        var response = await PostWithRefreshCookieAsync("logout", refreshToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var cleared = RefreshCookieFrom(response);
        cleared.Value.ToString().ShouldBeEmpty();
        cleared.Path.ToString().ShouldBe(AuthController.RefreshTokenCookiePath);
        cleared.Expires!.Value.ShouldBeLessThan(DateTimeOffset.UtcNow);
        await ShouldBeProblemAsync(await PostWithRefreshCookieAsync("refresh", refreshToken), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_without_cookie_returns_204()
    {
        var response = await PostWithRefreshCookieAsync("logout", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
