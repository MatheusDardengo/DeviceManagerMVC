using DeviceManagerMVC.Api.Contracts;
using DeviceManagerMVC.Application.Dtos;
using DeviceManagerMVC.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeviceManagerMVC.Api.Controllers;

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
    public Task<ActionResult<UserResponse>> Register(RegisterRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    /// <summary>
    /// Logs in. Returns the access token in the body and sets the refresh token cookie; 401 for invalid credentials.
    /// </summary>
    [HttpPost("login")]
    public Task<ActionResult<AccessTokenResponse>> Login(LoginRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    /// <summary>
    /// Exchanges the refresh token cookie for a new access token and a rotated cookie; 401 when it is invalid.
    /// </summary>
    [HttpPost("refresh")]
    public Task<ActionResult<AccessTokenResponse>> Refresh(CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    /// <summary>
    /// Revokes the refresh token session and clears the cookie. Always returns 204.
    /// </summary>
    [HttpPost("logout")]
    public Task<IActionResult> Logout(CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
