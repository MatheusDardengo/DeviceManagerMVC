namespace DeviceManagerMVC.Api.Contracts;

/// <summary>Body of login/refresh. The refresh token never appears here: it travels only in an HttpOnly cookie.</summary>
public sealed record AccessTokenResponse(string AccessToken, DateTime ExpiresAtUtc);
