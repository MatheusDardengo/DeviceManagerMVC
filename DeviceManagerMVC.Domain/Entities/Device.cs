using DeviceManagerMVC.Domain.Common;
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

    public static Device Create(string name, string brand, DeviceState state, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        var validName = ValidateText(name, nameof(Name), NameMaxLength);
        var validBrand = ValidateText(brand, nameof(Brand), BrandMaxLength);
        ValidateState(state);

        var now = timeProvider.GetUtcNow().UtcDateTime;

        return new Device
        {
            Name = validName,
            Brand = validBrand,
            State = state,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    /// <summary>
    /// Restores a device that already exists in storage. Not a creation path:
    /// no validation and no clock, the values are trusted as persisted.
    /// </summary>
    public static Device Rehydrate(
        int id, string name, string brand, DeviceState state, DateTime createdAtUtc, DateTime updatedAtUtc) =>
        new()
        {
            Id = id,
            Name = name,
            Brand = brand,
            State = state,
            // DATETIME2 comes back as Unspecified; the columns are UTC by convention.
            CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc),
            UpdatedAtUtc = DateTime.SpecifyKind(updatedAtUtc, DateTimeKind.Utc)
        };

    public void Update(string name, string brand, DeviceState state, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        // Validate everything before assigning anything, so a failed update leaves the device untouched.
        var validName = ValidateText(name, nameof(Name), NameMaxLength);
        var validBrand = ValidateText(brand, nameof(Brand), BrandMaxLength);
        ValidateState(state);

        // Checked against the current state: a device must be released before it can be renamed.
        if (State == DeviceState.InUse && (validName != Name || validBrand != Brand))
        {
            throw new BusinessRuleViolationException("Name and brand cannot be changed while the device is in use.");
        }

        Name = validName;
        Brand = validBrand;
        State = state;
        UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
    }

    public void EnsureCanBeDeleted()
    {
        if (State == DeviceState.InUse)
        {
            throw new BusinessRuleViolationException("A device in use cannot be deleted.");
        }
    }

    private static string ValidateText(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException($"{field} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainValidationException($"{field} must be at most {maxLength} characters.");
        }

        return trimmed;
    }

    private static void ValidateState(DeviceState state)
    {
        if (!Enum.IsDefined(state))
        {
            throw new DomainValidationException($"State '{(int)state}' is not a valid device state.");
        }
    }
}
