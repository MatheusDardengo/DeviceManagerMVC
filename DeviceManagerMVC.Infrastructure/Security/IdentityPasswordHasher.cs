using DeviceManagerMVC.Application.Abstractions;

namespace DeviceManagerMVC.Infrastructure.Security;

public sealed class IdentityPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => throw new NotImplementedException();

    public bool Verify(string passwordHash, string password) => throw new NotImplementedException();
}
