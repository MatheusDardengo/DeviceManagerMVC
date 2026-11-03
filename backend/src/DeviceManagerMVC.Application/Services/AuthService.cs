using DeviceManagerMVC.Application.Abstractions;
using DeviceManagerMVC.Application.Common;
using DeviceManagerMVC.Application.Dtos;
using DeviceManagerMVC.Domain.Common;
using DeviceManagerMVC.Domain.Entities;
using DeviceManagerMVC.Domain.Repositories;

namespace DeviceManagerMVC.Application.Services;

/// <summary>
/// Auth use cases. Refresh tokens rotate on every use; presenting a token that was already
/// rotated (or losing a concurrent rotation) is treated as theft and revokes the whole family.
/// </summary>
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

    // Same message for unknown email and wrong password: no account enumeration.
    private const string InvalidCredentials = "Invalid email or password.";
    private const string InvalidRefreshToken = "Invalid or expired refresh token.";

    public async Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        ValidatePassword(request.Password);

        var user = User.Create(request.Email, passwordHasher.Hash(request.Password), timeProvider);

        if (await users.GetByEmailAsync(user.Email, cancellationToken) is not null)
        {
            throw new BusinessRuleViolationException("Email is already registered.");
        }

        var id = await users.AddAsync(user, cancellationToken);

        return new UserResponse(id, user.Email, user.CreatedAtUtc);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            throw new UnauthorizedException(InvalidCredentials);
        }

        var user = await users.GetByEmailAsync(User.NormalizeEmail(request.Email), cancellationToken);
        if (user is null || !passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            throw new UnauthorizedException(InvalidCredentials);
        }

        var generated = refreshTokenGenerator.Generate();
        var refreshToken = RefreshToken.StartFamily(user.Id, generated.Hash, RefreshTokenLifetime, timeProvider);
        await refreshTokens.AddAsync(refreshToken, cancellationToken);

        return ToResult(accessTokenIssuer.Issue(user), generated.RawToken, refreshToken);
    }

    public async Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        var current = await FindAsync(refreshToken, cancellationToken)
            ?? throw new UnauthorizedException(InvalidRefreshToken);

        if (current.IsRevoked)
        {
            await RevokeFamilyAsync(current, cancellationToken);
            throw new UnauthorizedException(InvalidRefreshToken);
        }

        if (current.IsExpired(timeProvider))
        {
            throw new UnauthorizedException(InvalidRefreshToken);
        }

        var user = await users.GetByIdAsync(current.UserId, cancellationToken)
            ?? throw new UnauthorizedException(InvalidRefreshToken);

        var generated = refreshTokenGenerator.Generate();
        var replacement = current.Rotate(generated.Hash, RefreshTokenLifetime, timeProvider);

        if (!await refreshTokens.RotateAsync(current, replacement, cancellationToken))
        {
            await RevokeFamilyAsync(current, cancellationToken);
            throw new UnauthorizedException(InvalidRefreshToken);
        }

        return ToResult(accessTokenIssuer.Issue(user), generated.RawToken, replacement);
    }

    /// <summary>Idempotent: a missing or unknown token is not an error.</summary>
    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (await FindAsync(refreshToken, cancellationToken) is { } current)
        {
            await RevokeFamilyAsync(current, cancellationToken);
        }
    }

    /// <summary>Profile of the authenticated user; 401 when the account no longer exists.</summary>
    public async Task<UserResponse> GetCurrentUserAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedException("User no longer exists.");

        return new UserResponse(user.Id, user.Email, user.CreatedAtUtc);
    }

    private static void ValidatePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new DomainValidationException("Password is required.");
        }

        if (password.Length < PasswordMinLength)
        {
            throw new DomainValidationException($"Password must be at least {PasswordMinLength} characters.");
        }
    }

    private async Task<RefreshToken?> FindAsync(string? rawToken, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(rawToken)
            ? null
            : await refreshTokens.GetByHashAsync(refreshTokenGenerator.ComputeHash(rawToken), cancellationToken);

    private Task RevokeFamilyAsync(RefreshToken token, CancellationToken cancellationToken) =>
        refreshTokens.RevokeFamilyAsync(token.FamilyId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

    private static AuthResult ToResult(AccessToken accessToken, string rawRefreshToken, RefreshToken refreshToken) =>
        new(accessToken.Token, accessToken.ExpiresAtUtc, rawRefreshToken, refreshToken.ExpiresAtUtc);
}
