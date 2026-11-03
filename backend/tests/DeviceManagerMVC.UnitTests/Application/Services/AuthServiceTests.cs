using DeviceManagerMVC.Application.Abstractions;
using DeviceManagerMVC.Application.Common;
using DeviceManagerMVC.Application.Dtos;
using DeviceManagerMVC.Application.Services;
using DeviceManagerMVC.Domain.Common;
using DeviceManagerMVC.Domain.Entities;
using DeviceManagerMVC.Domain.Repositories;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace DeviceManagerMVC.UnitTests.Application.Services;

public sealed class AuthServiceTests
{
    private const int UserId = 5;
    private const string Email = "user@example.com";
    private const string Password = "Passw0rd!";
    private const string StoredRawToken = "raw-1";
    private const string NewRawToken = "raw-2";

    private static readonly DateTimeOffset Now = new(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly AccessToken IssuedAccessToken = new("jwt", Now.AddMinutes(15).UtcDateTime);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IAccessTokenIssuer _issuer = Substitute.For<IAccessTokenIssuer>();
    private readonly IRefreshTokenGenerator _generator = Substitute.For<IRefreshTokenGenerator>();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly CancellationToken _ct = new CancellationTokenSource().Token;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(_users, _refreshTokens, _hasher, _issuer, _generator, _time);

        // Fake hasher: deterministic, and the "hash" is visibly different from the password.
        _hasher.Hash(Arg.Any<string>()).Returns(ci => HashedPassword(ci.Arg<string>()));
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>())
            .Returns(ci => ci.ArgAt<string>(0) == HashedPassword(ci.ArgAt<string>(1)));

        _issuer.Issue(Arg.Any<User>()).Returns(IssuedAccessToken);
        _generator.Generate().Returns(new GeneratedRefreshToken(NewRawToken, HashOf(2)));
        _generator.ComputeHash(StoredRawToken).Returns(HashOf(1));
        _refreshTokens.RotateAsync(Arg.Any<RefreshToken>(), Arg.Any<RefreshToken>(), _ct).Returns(true);
    }

    private static string HashedPassword(string password) => $"hashed:{password}";

    private static byte[] HashOf(byte seed) => Enumerable.Repeat(seed, RefreshToken.TokenHashLength).ToArray();

    private static byte[] SameBytes(byte[] expected) => Arg.Is<byte[]>(h => h.SequenceEqual(expected));

    private User GivenStoredUser()
    {
        var user = User.Rehydrate(UserId, Email, HashedPassword(Password), Now.UtcDateTime);
        _users.GetByEmailAsync(Email, _ct).Returns(user);
        _users.GetByIdAsync(UserId, _ct).Returns(user);
        return user;
    }

    /// <summary>An active token issued at <see cref="Now"/>, findable by <see cref="StoredRawToken"/>.</summary>
    private RefreshToken GivenStoredRefreshToken()
    {
        var token = RefreshToken.StartFamily(UserId, HashOf(1), AuthService.RefreshTokenLifetime, _time);
        _refreshTokens.GetByHashAsync(SameBytes(HashOf(1)), _ct).Returns(token);
        return token;
    }

    private async Task ShouldNotHaveIssuedTokens()
    {
        _issuer.DidNotReceiveWithAnyArgs().Issue(default!);
        await _refreshTokens.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _refreshTokens.DidNotReceiveWithAnyArgs().RotateAsync(default!, default!, default);
    }

    // ---------- Register ----------

    [Fact]
    public async Task Register_hashes_password_persists_user_and_returns_it()
    {
        _users.AddAsync(Arg.Any<User>(), _ct).Returns(10);

        var response = await _sut.RegisterAsync(new RegisterRequest("  User@Example.com ", Password), _ct);

        response.ShouldBe(new UserResponse(10, Email, Now.UtcDateTime));
        await _users.Received(1).AddAsync(
            Arg.Is<User>(u => u.Email == Email && u.PasswordHash == HashedPassword(Password)), _ct);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("        ")]
    [InlineData("1234567")]
    public async Task Register_rejects_missing_or_short_password_before_hashing(string? password)
    {
        await Should.ThrowAsync<DomainValidationException>(
            () => _sut.RegisterAsync(new RegisterRequest(Email, password!), _ct));

        _hasher.DidNotReceiveWithAnyArgs().Hash(default!);
        await _users.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Register_accepts_password_at_min_length()
    {
        var password = new string('x', AuthService.PasswordMinLength);

        await _sut.RegisterAsync(new RegisterRequest(Email, password), _ct);

        await _users.Received(1).AddAsync(Arg.Is<User>(u => u.PasswordHash == HashedPassword(password)), _ct);
    }

    [Fact]
    public async Task Register_rejects_invalid_email()
    {
        await Should.ThrowAsync<DomainValidationException>(
            () => _sut.RegisterAsync(new RegisterRequest("not-an-email", Password), _ct));

        await _users.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Register_rejects_email_already_registered_regardless_of_case()
    {
        GivenStoredUser();

        await Should.ThrowAsync<BusinessRuleViolationException>(
            () => _sut.RegisterAsync(new RegisterRequest("USER@example.com", Password), _ct));

        await _users.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    // ---------- Login ----------

    [Fact]
    public async Task Login_returns_access_token_and_starts_a_new_refresh_token_family()
    {
        var user = GivenStoredUser();

        var result = await _sut.LoginAsync(new LoginRequest(" User@Example.com ", Password), _ct);

        result.ShouldBe(new AuthResult(
            IssuedAccessToken.Token,
            IssuedAccessToken.ExpiresAtUtc,
            NewRawToken,
            Now.Add(AuthService.RefreshTokenLifetime).UtcDateTime));
        _issuer.Received(1).Issue(user);
        await _refreshTokens.Received(1).AddAsync(
            Arg.Is<RefreshToken>(t =>
                t.UserId == UserId &&
                t.TokenHash.SequenceEqual(HashOf(2)) &&
                t.ExpiresAtUtc == Now.Add(AuthService.RefreshTokenLifetime).UtcDateTime &&
                !t.IsRevoked),
            _ct);
    }

    [Fact]
    public async Task Login_with_unknown_email_throws_Unauthorized()
    {
        await Should.ThrowAsync<UnauthorizedException>(
            () => _sut.LoginAsync(new LoginRequest("nobody@example.com", Password), _ct));

        await ShouldNotHaveIssuedTokens();
    }

    [Fact]
    public async Task Login_with_wrong_password_throws_Unauthorized()
    {
        GivenStoredUser();

        await Should.ThrowAsync<UnauthorizedException>(
            () => _sut.LoginAsync(new LoginRequest(Email, "wrong-password"), _ct));

        await ShouldNotHaveIssuedTokens();
    }

    [Fact]
    public async Task Login_failure_message_does_not_reveal_whether_the_email_exists()
    {
        GivenStoredUser();

        var unknownEmail = await Should.ThrowAsync<UnauthorizedException>(
            () => _sut.LoginAsync(new LoginRequest("nobody@example.com", Password), _ct));
        var wrongPassword = await Should.ThrowAsync<UnauthorizedException>(
            () => _sut.LoginAsync(new LoginRequest(Email, "wrong-password"), _ct));

        unknownEmail.Message.ShouldBe(wrongPassword.Message);
    }

    // ---------- Refresh ----------

    [Fact]
    public async Task Refresh_rotates_token_within_the_family_and_returns_a_new_pair()
    {
        var user = GivenStoredUser();
        var stored = GivenStoredRefreshToken();
        _time.Advance(TimeSpan.FromMinutes(10));

        var result = await _sut.RefreshAsync(StoredRawToken, _ct);

        result.ShouldBe(new AuthResult(
            IssuedAccessToken.Token,
            IssuedAccessToken.ExpiresAtUtc,
            NewRawToken,
            Now.AddMinutes(10).Add(AuthService.RefreshTokenLifetime).UtcDateTime));
        stored.IsRevoked.ShouldBeTrue();
        await _refreshTokens.Received(1).RotateAsync(
            stored,
            Arg.Is<RefreshToken>(r => r.TokenHash.SequenceEqual(HashOf(2)) && r.FamilyId == stored.FamilyId),
            _ct);
        _issuer.Received(1).Issue(user);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Refresh_without_token_throws_Unauthorized(string? refreshToken)
    {
        await Should.ThrowAsync<UnauthorizedException>(() => _sut.RefreshAsync(refreshToken, _ct));

        await ShouldNotHaveIssuedTokens();
    }

    [Fact]
    public async Task Refresh_with_unknown_token_throws_Unauthorized()
    {
        GivenStoredUser();

        await Should.ThrowAsync<UnauthorizedException>(() => _sut.RefreshAsync(StoredRawToken, _ct));

        await ShouldNotHaveIssuedTokens();
    }

    [Fact]
    public async Task Refresh_with_expired_token_throws_Unauthorized_without_revoking_the_family()
    {
        GivenStoredUser();
        GivenStoredRefreshToken();
        _time.Advance(AuthService.RefreshTokenLifetime);

        await Should.ThrowAsync<UnauthorizedException>(() => _sut.RefreshAsync(StoredRawToken, _ct));

        await ShouldNotHaveIssuedTokens();
        await _refreshTokens.DidNotReceiveWithAnyArgs().RevokeFamilyAsync(default, default, default);
    }

    [Fact]
    public async Task Refresh_reusing_a_revoked_token_revokes_the_whole_family()
    {
        GivenStoredUser();
        var stored = GivenStoredRefreshToken();
        stored.Revoke(_time);
        _time.Advance(TimeSpan.FromMinutes(10));

        await Should.ThrowAsync<UnauthorizedException>(() => _sut.RefreshAsync(StoredRawToken, _ct));

        await _refreshTokens.Received(1).RevokeFamilyAsync(stored.FamilyId, Now.AddMinutes(10).UtcDateTime, _ct);
        await ShouldNotHaveIssuedTokens();
    }

    [Fact]
    public async Task Refresh_losing_a_concurrent_rotation_is_treated_as_reuse()
    {
        GivenStoredUser();
        var stored = GivenStoredRefreshToken();
        _refreshTokens.RotateAsync(stored, Arg.Any<RefreshToken>(), _ct).Returns(false);

        await Should.ThrowAsync<UnauthorizedException>(() => _sut.RefreshAsync(StoredRawToken, _ct));

        await _refreshTokens.Received(1).RevokeFamilyAsync(stored.FamilyId, Now.UtcDateTime, _ct);
        _issuer.DidNotReceiveWithAnyArgs().Issue(default!);
    }

    [Fact]
    public async Task Refresh_for_a_user_that_no_longer_exists_throws_Unauthorized()
    {
        GivenStoredRefreshToken();

        await Should.ThrowAsync<UnauthorizedException>(() => _sut.RefreshAsync(StoredRawToken, _ct));

        await ShouldNotHaveIssuedTokens();
    }

    // ---------- Logout ----------

    [Fact]
    public async Task Logout_revokes_the_whole_family()
    {
        var stored = GivenStoredRefreshToken();

        await _sut.LogoutAsync(StoredRawToken, _ct);

        await _refreshTokens.Received(1).RevokeFamilyAsync(stored.FamilyId, Now.UtcDateTime, _ct);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-token")]
    public async Task Logout_with_missing_or_unknown_token_is_a_no_op(string? refreshToken)
    {
        _generator.ComputeHash("unknown-token").Returns(HashOf(9));

        await _sut.LogoutAsync(refreshToken, _ct);

        await _refreshTokens.DidNotReceiveWithAnyArgs().RevokeFamilyAsync(default, default, default);
    }

    // ---------- Current user ----------

    [Fact]
    public async Task GetCurrentUser_returns_the_user_without_password_data()
    {
        GivenStoredUser();

        var response = await _sut.GetCurrentUserAsync(UserId, _ct);

        response.ShouldBe(new UserResponse(UserId, Email, Now.UtcDateTime));
    }

    [Fact]
    public async Task GetCurrentUser_for_a_user_that_no_longer_exists_throws_Unauthorized()
    {
        await Should.ThrowAsync<UnauthorizedException>(() => _sut.GetCurrentUserAsync(UserId, _ct));
    }
}
