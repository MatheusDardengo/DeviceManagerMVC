namespace DeviceManagerMVC.Application.Dtos;

public sealed record RegisterRequest(string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record UserResponse(int Id, string Email, DateTime CreatedAtUtc);

/// <summary>The API returns the access token in the body and the refresh token as a cookie.</summary>
public sealed record AuthResult(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);
