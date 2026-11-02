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

    public bool IsRevoked => RevokedAtUtc is not null;

    public bool IsExpired(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return timeProvider.GetUtcNow().UtcDateTime >= ExpiresAtUtc;
    }

    public bool IsActive(TimeProvider timeProvider) => !IsRevoked && !IsExpired(timeProvider);

    /// <summary>First token of a new login: starts a new family.</summary>
    public static RefreshToken StartFamily(int userId, byte[] tokenHash, TimeSpan lifetime, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ValidateHash(tokenHash, nameof(tokenHash));
        ValidateLifetime(lifetime);

        return Issue(userId, tokenHash, Guid.NewGuid(), lifetime, timeProvider);
    }

    /// <summary>
    /// Restores a token that already exists in storage. Not a creation path:
    /// no validation and no clock, the values are trusted as persisted.
    /// </summary>
    public static RefreshToken Rehydrate(
        int userId,
        byte[] tokenHash,
        Guid familyId,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        DateTime? revokedAtUtc,
        byte[]? replacedByTokenHash) =>
        new()
        {
            UserId = userId,
            TokenHash = tokenHash,
            FamilyId = familyId,
            CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc),
            ExpiresAtUtc = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc),
            RevokedAtUtc = revokedAtUtc is { } revoked ? DateTime.SpecifyKind(revoked, DateTimeKind.Utc) : null,
            ReplacedByTokenHash = replacedByTokenHash
        };

    /// <summary>
    /// Revokes this token, links it to its replacement and returns the replacement (same family).
    /// The caller must check <see cref="IsActive"/> first; rotating an inactive token is a bug.
    /// </summary>
    public RefreshToken Rotate(byte[] newTokenHash, TimeSpan lifetime, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ValidateHash(newTokenHash, nameof(newTokenHash));
        ValidateLifetime(lifetime);

        if (!IsActive(timeProvider))
        {
            throw new InvalidOperationException("Only an active refresh token can be rotated.");
        }

        var replacement = Issue(UserId, newTokenHash, FamilyId, lifetime, timeProvider);

        Revoke(timeProvider);
        ReplacedByTokenHash = replacement.TokenHash.ToArray();

        return replacement;
    }

    /// <summary>Idempotent: revoking twice keeps the first revocation time.</summary>
    public void Revoke(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        RevokedAtUtc ??= timeProvider.GetUtcNow().UtcDateTime;
    }

    private static RefreshToken Issue(int userId, byte[] tokenHash, Guid familyId, TimeSpan lifetime, TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        return new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash.ToArray(),
            FamilyId = familyId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(lifetime)
        };
    }

    private static void ValidateHash(byte[] hash, string paramName)
    {
        ArgumentNullException.ThrowIfNull(hash, paramName);

        if (hash.Length != TokenHashLength)
        {
            throw new ArgumentException($"Token hash must be {TokenHashLength} bytes (SHA-256).", paramName);
        }
    }

    private static void ValidateLifetime(TimeSpan lifetime) =>
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);
}
