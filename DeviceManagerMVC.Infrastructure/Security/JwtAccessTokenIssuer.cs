using DeviceManagerMVC.Application.Abstractions;
using DeviceManagerMVC.Domain.Entities;

namespace DeviceManagerMVC.Infrastructure.Security;

public sealed class JwtAccessTokenIssuer(JwtOptions options, TimeProvider timeProvider) : IAccessTokenIssuer
{
    public const int MinimumKeySizeInBytes = 32;

    public AccessToken Issue(User user) => throw new NotImplementedException();
}
