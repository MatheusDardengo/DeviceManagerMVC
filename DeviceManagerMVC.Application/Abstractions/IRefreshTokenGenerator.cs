namespace DeviceManagerMVC.Application.Abstractions;

/// <summary>The raw token goes to the client; only <paramref name="Hash"/> is ever stored.</summary>
public sealed record GeneratedRefreshToken(string RawToken, byte[] Hash);

public interface IRefreshTokenGenerator
{
    GeneratedRefreshToken Generate();

    byte[] ComputeHash(string rawToken);
}
