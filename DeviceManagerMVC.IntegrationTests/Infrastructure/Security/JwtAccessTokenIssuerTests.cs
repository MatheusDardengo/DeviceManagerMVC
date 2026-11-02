using System.Text;
using DeviceManagerMVC.Domain.Entities;
using DeviceManagerMVC.Infrastructure.Security;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace DeviceManagerMVC.IntegrationTests.Infrastructure.Security;

public sealed class JwtAccessTokenIssuerTests
{
    private const string SigningKey = "test-signing-key-with-at-least-32-bytes!";

    private static readonly DateTimeOffset Now = new(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly JwtOptions Options = new()
    {
        Issuer = "device-manager",
        Audience = "device-manager-spa",
        SigningKey = SigningKey
    };

    private readonly FakeTimeProvider _time = new(Now);
    private readonly User _user = User.Rehydrate(5, "user@example.com", "hash", Now.UtcDateTime);
    private readonly JwtAccessTokenIssuer _sut;

    public JwtAccessTokenIssuerTests()
    {
        _sut = new JwtAccessTokenIssuer(Options, _time);
    }

    private static TokenValidationParameters ValidationParameters(string signingKey = SigningKey) => new()
    {
        ValidIssuer = Options.Issuer,
        ValidAudience = Options.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        // The token is issued on the fake clock; expiry is asserted explicitly below.
        ValidateLifetime = false
    };

    [Fact]
    public void Issue_expires_after_the_configured_lifetime_of_15_minutes()
    {
        var token = _sut.Issue(_user);

        token.ExpiresAtUtc.ShouldBe(Now.AddMinutes(15).UtcDateTime);
        new JsonWebToken(token.Token).ValidTo.ShouldBe(Now.AddMinutes(15).UtcDateTime);
    }

    [Fact]
    public void Issue_signs_with_HS256_and_carries_user_id_and_email()
    {
        var jwt = new JsonWebToken(_sut.Issue(_user).Token);

        jwt.Alg.ShouldBe(SecurityAlgorithms.HmacSha256);
        jwt.Subject.ShouldBe("5");
        jwt.GetClaim(JwtRegisteredClaimNames.Email).Value.ShouldBe("user@example.com");
        jwt.Issuer.ShouldBe(Options.Issuer);
        jwt.Audiences.ShouldBe([Options.Audience]);
        jwt.IssuedAt.ShouldBe(Now.UtcDateTime);
    }

    [Fact]
    public async Task Issue_produces_a_token_that_validates_with_the_signing_key()
    {
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(_sut.Issue(_user).Token, ValidationParameters());

        result.IsValid.ShouldBeTrue(result.Exception?.Message);
    }

    [Fact]
    public async Task Issue_produces_a_token_that_fails_validation_with_another_key()
    {
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            _sut.Issue(_user).Token, ValidationParameters("another-signing-key-with-32-bytes-or-more"));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_rejects_signing_key_shorter_than_256_bits()
    {
        var options = new JwtOptions { Issuer = Options.Issuer, Audience = Options.Audience, SigningKey = "too-short" };

        Should.Throw<ArgumentException>(() => new JwtAccessTokenIssuer(options, _time));
    }
}
