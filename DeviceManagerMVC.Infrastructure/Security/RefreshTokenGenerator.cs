using DeviceManagerMVC.Application.Abstractions;

namespace DeviceManagerMVC.Infrastructure.Security;

public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    public const int TokenSizeInBytes = 64;

    public GeneratedRefreshToken Generate() => throw new NotImplementedException();

    public byte[] ComputeHash(string rawToken) => throw new NotImplementedException();
}
