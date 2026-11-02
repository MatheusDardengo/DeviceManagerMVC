using DeviceManagerMVC.Application.Abstractions;
using DeviceManagerMVC.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace DeviceManagerMVC.Infrastructure.Security;

/// <summary>
/// Adapter over ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/>
/// (PBKDF2-HMAC-SHA512, random salt, iteration count embedded in the hash).
/// </summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private static readonly PasswordHasher<User> Hasher = new();

    // The default implementation never reads the user argument.
    public string Hash(string password) => Hasher.HashPassword(null!, password);

    // SuccessRehashNeeded still means the password is correct.
    public bool Verify(string passwordHash, string password) =>
        Hasher.VerifyHashedPassword(null!, passwordHash, password) != PasswordVerificationResult.Failed;
}
