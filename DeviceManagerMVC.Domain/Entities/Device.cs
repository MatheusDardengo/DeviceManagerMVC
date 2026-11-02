using DeviceManagerMVC.Domain.Enums;

namespace DeviceManagerMVC.Domain.Entities;

public sealed class Device
{
    public const int NameMaxLength = 100;
    public const int BrandMaxLength = 100;

    private Device()
    {
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Brand { get; private set; } = string.Empty;
    public DeviceState State { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static Device Create(string name, string brand, DeviceState state, TimeProvider timeProvider) =>
        throw new NotImplementedException();

    public void Update(string name, string brand, DeviceState state, TimeProvider timeProvider) =>
        throw new NotImplementedException();

    public void EnsureCanBeDeleted() =>
        throw new NotImplementedException();
}
