using DeviceManagerMVC.Application.Common;
using DeviceManagerMVC.Application.Dtos;
using DeviceManagerMVC.Domain.Entities;
using DeviceManagerMVC.Domain.Enums;
using DeviceManagerMVC.Domain.Repositories;

namespace DeviceManagerMVC.Application.Services;

/// <summary>
/// Device use cases. Orchestrates load → domain call → persist; every business rule
/// lives in <see cref="Device"/>, so a rejected change never reaches the repository.
/// </summary>
public sealed class DeviceService(IDeviceRepository repository, TimeProvider timeProvider)
{
    public async Task<DeviceResponse> CreateAsync(CreateDeviceRequest request, CancellationToken cancellationToken)
    {
        var device = Device.Create(request.Name, request.Brand, request.State, timeProvider);

        var id = await repository.AddAsync(device, cancellationToken);

        return ToResponse(id, device);
    }

    public async Task<DeviceResponse> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var device = await GetExistingAsync(id, cancellationToken);

        return ToResponse(id, device);
    }

    public async Task<IReadOnlyList<DeviceResponse>> ListAsync(
        string? brand, DeviceState? state, CancellationToken cancellationToken)
    {
        var devices = await repository.ListAsync(brand, state, cancellationToken);

        return devices.Select(device => ToResponse(device.Id, device)).ToList();
    }

    public async Task<DeviceResponse> UpdateAsync(int id, UpdateDeviceRequest request, CancellationToken cancellationToken)
    {
        var device = await GetExistingAsync(id, cancellationToken);

        device.Update(request.Name, request.Brand, request.State, timeProvider);
        await repository.UpdateAsync(id, device, cancellationToken);

        return ToResponse(id, device);
    }

    public async Task<DeviceResponse> PatchAsync(int id, PatchDeviceRequest request, CancellationToken cancellationToken)
    {
        var device = await GetExistingAsync(id, cancellationToken);

        // Missing fields keep their current value; the entity still enforces every rule.
        device.Update(
            request.Name ?? device.Name,
            request.Brand ?? device.Brand,
            request.State ?? device.State,
            timeProvider);
        await repository.UpdateAsync(id, device, cancellationToken);

        return ToResponse(id, device);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var device = await GetExistingAsync(id, cancellationToken);

        device.EnsureCanBeDeleted();
        await repository.DeleteAsync(id, cancellationToken);
    }

    private async Task<Device> GetExistingAsync(int id, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Device {id} was not found.");

    private static DeviceResponse ToResponse(int id, Device device) =>
        new(id, device.Name, device.Brand, device.State, device.CreatedAtUtc, device.UpdatedAtUtc);
}
