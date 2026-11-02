using DeviceManagerMVC.Application.Abstractions;
using DeviceManagerMVC.Application.Dtos;
using DeviceManagerMVC.Domain.Repositories;

namespace DeviceManagerMVC.Application.Services;

public sealed class AuthService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer accessTokenIssuer,
    IRefreshTokenGenerator refreshTokenGenerator,
    TimeProvider timeProvider)
{
    public const int PasswordMinLength = 8;

    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    public Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
