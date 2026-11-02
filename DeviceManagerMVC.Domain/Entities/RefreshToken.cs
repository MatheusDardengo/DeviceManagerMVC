namespace DeviceManagerMVC.Domain.Entities;

/// <summary>
/// Stored side of a refresh token: only the SHA-256 hash of the raw token is kept.
/// All tokens issued from one login share a <see cref="FamilyId"/>, so reuse of an
/// already-rotated token can revoke the whole chain.
/// </summary>
public sealed class RefreshToken
{
    public const int TokenHashLength = 32; // SHA-256

    private RefreshToken()
    {
    }

    public int UserId { get; private set; }
    public byte[] TokenHash { get; private set; } = [];
    public Guid FamilyId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public byte[]? ReplacedByTokenHash { get; private set; }

    public bool IsRevoked => throw new NotImplementedException();

    public bool IsExpired(TimeProvider timeProvider) =>
        throw new NotImplementedException();

    public bool IsActive(TimeProvider timeProvider) =>
        throw new NotImplementedException();

    /// <summary>First token of a new login: starts a new family.</summary>
    public static RefreshToken StartFamily(int userId, byte[] tokenHash, TimeSpan lifetime, TimeProvider timeProvider) =>
        throw new NotImplementedException();

    /// <summary>Restores a token that already exists in storage.</summary>
    public static RefreshToken Rehydrate(
        int userId,
        byte[] tokenHash,
        Guid familyId,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        DateTime? revokedAtUtc,
        byte[]? replacedByTokenHash) =>
        throw new NotImplementedException();

    /// <summary>
    /// Revokes this token, links it to its replacement and returns the replacement (same family).
    /// The caller must check <see cref="IsActive"/> first; rotating an inactive token is a bug.
    /// </summary>
    public RefreshToken Rotate(byte[] newTokenHash, TimeSpan lifetime, TimeProvider timeProvider) =>
        throw new NotImplementedException();

    /// <summary>Idempotent: revoking twice keeps the first revocation time.</summary>
    public void Revoke(TimeProvider timeProvider) =>
        throw new NotImplementedException();
}
