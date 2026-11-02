using DeviceManagerMVC.Domain.Entities;
using DeviceManagerMVC.Domain.Enums;

namespace DeviceManagerMVC.Domain.Repositories;

public interface IDeviceRepository
{
    Task<Device?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>Filters are optional; null means "do not filter".</summary>
    Task<IReadOnlyList<Device>> ListAsync(string? brand, DeviceState? state, CancellationToken cancellationToken);

    /// <summary>Inserts the device and returns the database-generated id.</summary>
    Task<int> AddAsync(Device device, CancellationToken cancellationToken);

    Task UpdateAsync(int id, Device device, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
