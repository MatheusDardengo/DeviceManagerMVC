using DeviceManagerMVC.Domain.Enums;

namespace DeviceManagerMVC.Application.Dtos;

public sealed record CreateDeviceRequest(string Name, string Brand, DeviceState State);

/// <summary>Full replacement (PUT): every field is required.</summary>
public sealed record UpdateDeviceRequest(string Name, string Brand, DeviceState State);

/// <summary>Partial update (PATCH): null means "keep the current value".</summary>
public sealed record PatchDeviceRequest(string? Name, string? Brand, DeviceState? State);

public sealed record DeviceResponse(
    int Id,
    string Name,
    string Brand,
    DeviceState State,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
