namespace DeviceManagerMVC.Application.Common;

/// <summary>Credentials or refresh token are invalid. Maps to 401.</summary>
public sealed class UnauthorizedException(string message) : Exception(message);
