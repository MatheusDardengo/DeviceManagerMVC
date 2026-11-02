using DeviceManagerMVC.Api.Contracts;
using DeviceManagerMVC.Application.Dtos;
using DeviceManagerMVC.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeviceManagerMVC.Api.Controllers;

/// <summary>
/// HTTP adapter for <see cref="AuthService"/>. Its only extra job is moving the refresh token
/// in and out of an HttpOnly cookie, so the SPA's JavaScript never sees it.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService authService) : ControllerBase
{
    public const string RefreshTokenCookieName = "refreshToken";
    public const string RefreshTokenCookiePath = "/api/auth";

    /// <summary>
    /// Registers a new user. Returns 201 with the user; 400 for invalid input, 409 when the email is taken.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<UserResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var user = await authService.RegisterAsync(request, cancellationToken);

        // No GET endpoint for users exists, so there is no Location to point to.
        return StatusCode(StatusCodes.Status201Created, user);
    }

    /// <summary>
    /// Logs in. Returns the access token in the body and sets the refresh token cookie; 401 for invalid credentials.
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<AccessTokenResponse>> Login(LoginRequest request, CancellationToken cancellationToken) =>
        Ok(WithRefreshCookie(await authService.LoginAsync(request, cancellationToken)));

    /// <summary>
    /// Exchanges the refresh token cookie for a new access token and a rotated cookie; 401 when it is invalid.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<AccessTokenResponse>> Refresh(CancellationToken cancellationToken) =>
        Ok(WithRefreshCookie(await authService.RefreshAsync(Request.Cookies[RefreshTokenCookieName], cancellationToken)));

    /// <summary>
    /// Revokes the refresh token session and clears the cookie. Always returns 204.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(Request.Cookies[RefreshTokenCookieName], cancellationToken);

        // Delete must repeat the path (and flags) the cookie was set with, or the browser keeps it.
        Response.Cookies.Delete(RefreshTokenCookieName, RefreshCookieOptions(expiresAtUtc: null));

        return NoContent();
    }

    private AccessTokenResponse WithRefreshCookie(AuthResult result)
    {
        Response.Cookies.Append(
            RefreshTokenCookieName, result.RefreshToken, RefreshCookieOptions(result.RefreshTokenExpiresAtUtc));

        return new AccessTokenResponse(result.AccessToken, result.AccessTokenExpiresAtUtc);
    }

    // HttpOnly: unreadable by JavaScript (XSS). Secure: HTTPS only. SameSite=Strict + the /api/auth path:
    // never sent cross-site (CSRF) and never sent to any endpoint other than refresh/logout.
    private static CookieOptions RefreshCookieOptions(DateTime? expiresAtUtc) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = RefreshTokenCookiePath,
        Expires = expiresAtUtc is { } expires ? new DateTimeOffset(expires, TimeSpan.Zero) : null,
        IsEssential = true
    };
}
