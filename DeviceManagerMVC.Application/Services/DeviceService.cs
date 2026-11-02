using DeviceManagerMVC.Application.Dtos;
using DeviceManagerMVC.Domain.Enums;
using DeviceManagerMVC.Domain.Repositories;

namespace DeviceManagerMVC.Application.Services;

public sealed class DeviceService(IDeviceRepository repository, TimeProvider timeProvider)
{
    public Task<DeviceResponse> CreateAsync(CreateDeviceRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<DeviceResponse> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<DeviceResponse>> ListAsync(
        string? brand, DeviceState? state, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<DeviceResponse> UpdateAsync(int id, UpdateDeviceRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<DeviceResponse> PatchAsync(int id, PatchDeviceRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task DeleteAsync(int id, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
