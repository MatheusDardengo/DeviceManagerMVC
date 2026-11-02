using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using DeviceManagerMVC.Application.Abstractions;

namespace DeviceManagerMVC.Infrastructure.Security;

public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    public const int TokenSizeInBytes = 64;

    public GeneratedRefreshToken Generate()
    {
        // Base64Url: cookie-safe without escaping.
        var rawToken = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenSizeInBytes));

        return new GeneratedRefreshToken(rawToken, ComputeHash(rawToken));
    }

    // Plain SHA-256 is enough (no salt, no slow hash): the input is 512 random bits, not a guessable password.
    public byte[] ComputeHash(string rawToken)
    {
        ArgumentNullException.ThrowIfNull(rawToken);

        return SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
    }
}
