namespace DeviceManagerMVC.Domain.Entities;

public sealed class User
{
    public const int EmailMaxLength = 256;

    private User()
    {
    }

    public int Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Canonical form used for storage and lookup (trimmed, lower-case).</summary>
    public static string NormalizeEmail(string email) =>
        throw new NotImplementedException();

    /// <summary>Creates a new user. Receives the already-hashed password, never the plain text.</summary>
    public static User Create(string email, string passwordHash, TimeProvider timeProvider) =>
        throw new NotImplementedException();

    /// <summary>Restores a user that already exists in storage.</summary>
    public static User Rehydrate(int id, string email, string passwordHash, DateTime createdAtUtc) =>
        throw new NotImplementedException();
}
