namespace DeviceManagerMVC.Application.Common;

/// <summary>The requested resource does not exist. Maps to 404.</summary>
public sealed class NotFoundException(string message) : Exception(message);
