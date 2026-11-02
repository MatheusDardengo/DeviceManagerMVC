using System.Net.Mail;
using DeviceManagerMVC.Domain.Common;

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
    public static string NormalizeEmail(string email)
    {
        ArgumentNullException.ThrowIfNull(email);

        return email.Trim().ToLowerInvariant();
    }

    /// <summary>Creates a new user. Receives the already-hashed password, never the plain text.</summary>
    public static User Create(string email, string passwordHash, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        // The hash comes from the hasher, not from the client: a missing one is a bug, not a 400.
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        }

        return new User
        {
            Email = ValidateEmail(email),
            PasswordHash = passwordHash,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        };
    }

    /// <summary>
    /// Restores a user that already exists in storage. Not a creation path:
    /// no validation and no clock, the values are trusted as persisted.
    /// </summary>
    public static User Rehydrate(int id, string email, string passwordHash, DateTime createdAtUtc) =>
        new()
        {
            Id = id,
            Email = email,
            PasswordHash = passwordHash,
            // DATETIME2 comes back as Unspecified; the column is UTC by convention.
            CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc)
        };

    private static string ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainValidationException("Email is required.");
        }

        var normalized = NormalizeEmail(email);
        if (normalized.Length > EmailMaxLength)
        {
            throw new DomainValidationException($"Email must be at most {EmailMaxLength} characters.");
        }

        // MailAddress also accepts "Name <address>"; requiring the parsed address to equal
        // the input rejects that and anything else that is not a bare address.
        if (!MailAddress.TryCreate(normalized, out var address) || address.Address != normalized)
        {
            throw new DomainValidationException("Email is not a valid email address.");
        }

        return normalized;
    }
}
