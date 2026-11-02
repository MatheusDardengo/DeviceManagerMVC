using DeviceManagerMVC.Domain.Entities;
using DeviceManagerMVC.Domain.Enums;
using DeviceManagerMVC.Domain.Repositories;
using DeviceManagerMVC.Infrastructure.Persistence;

namespace DeviceManagerMVC.Infrastructure.Repositories;

public sealed class DeviceRepository(SqlConnectionFactory connectionFactory) : IDeviceRepository
{
    public Task<Device?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<Device>> ListAsync(string? brand, DeviceState? state, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<int> AddAsync(Device device, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task UpdateAsync(int id, Device device, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task DeleteAsync(int id, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
