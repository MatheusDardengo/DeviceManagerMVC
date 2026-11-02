namespace DeviceManagerMVC.Infrastructure.Security;

/// <summary>Bound from the "Jwt" configuration section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; init; }
    public required string Audience { get; init; }

    /// <summary>HS256 key; at least 32 bytes (256 bits) once UTF-8 encoded.</summary>
    public required string SigningKey { get; init; }

    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);
}
