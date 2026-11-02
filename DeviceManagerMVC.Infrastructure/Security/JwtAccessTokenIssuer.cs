using System.Globalization;
using System.Text;
using DeviceManagerMVC.Application.Abstractions;
using DeviceManagerMVC.Domain.Entities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DeviceManagerMVC.Infrastructure.Security;

/// <summary>Issues short-lived HS256 access tokens. Times come from <see cref="TimeProvider"/>.</summary>
public sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
    public const int MinimumKeySizeInBytes = 32; // HS256 needs a key of at least 256 bits

    private readonly JsonWebTokenHandler _handler = new();
    private readonly JwtOptions _options;
    private readonly SigningCredentials _signingCredentials;
    private readonly TimeProvider _timeProvider;

    public JwtAccessTokenIssuer(JwtOptions options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        // Fail at startup with a clear message instead of on the first login.
        var key = Encoding.UTF8.GetBytes(options.SigningKey);
        if (key.Length < MinimumKeySizeInBytes)
        {
            throw new ArgumentException(
                $"JWT signing key must be at least {MinimumKeySizeInBytes} bytes.", nameof(options));
        }

        _options = options;
        _signingCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);
        _timeProvider = timeProvider;
    }

    public AccessToken Issue(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAtUtc = now.Add(_options.AccessTokenLifetime);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAtUtc,
            SigningCredentials = _signingCredentials,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(CultureInfo.InvariantCulture),
                [JwtRegisteredClaimNames.Email] = user.Email,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString()
            }
        });

        return new AccessToken(token, expiresAtUtc);
    }
}
