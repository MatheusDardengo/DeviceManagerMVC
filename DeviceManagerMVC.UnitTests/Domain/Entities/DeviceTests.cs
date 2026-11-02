using DeviceManagerMVC.Domain.Common;
using DeviceManagerMVC.Domain.Entities;
using DeviceManagerMVC.Domain.Enums;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace DeviceManagerMVC.UnitTests.Domain.Entities;

public sealed class DeviceTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(CreatedAt);

    private Device CreateDevice(DeviceState state = DeviceState.Available) =>
        Device.Create("iPhone 15", "Apple", state, _time);

    // ---------- Create ----------

    [Fact]
    public void Create_sets_properties_and_both_timestamps_to_now()
    {
        var device = Device.Create("iPhone 15", "Apple", DeviceState.InUse, _time);

        device.Name.ShouldBe("iPhone 15");
        device.Brand.ShouldBe("Apple");
        device.State.ShouldBe(DeviceState.InUse);
        device.CreatedAtUtc.ShouldBe(CreatedAt.UtcDateTime);
        device.UpdatedAtUtc.ShouldBe(CreatedAt.UtcDateTime);
    }

    [Fact]
    public void Create_trims_name_and_brand()
    {
        var device = Device.Create("  iPhone 15  ", "  Apple ", DeviceState.Available, _time);

        device.Name.ShouldBe("iPhone 15");
        device.Brand.ShouldBe("Apple");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_missing_name(string? name)
    {
        Should.Throw<DomainValidationException>(
            () => Device.Create(name!, "Apple", DeviceState.Available, _time));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_missing_brand(string? brand)
    {
        Should.Throw<DomainValidationException>(
            () => Device.Create("iPhone 15", brand!, DeviceState.Available, _time));
    }

    [Fact]
    public void Create_accepts_name_and_brand_at_max_length()
    {
        var device = Device.Create(
            new string('n', Device.NameMaxLength),
            new string('b', Device.BrandMaxLength),
            DeviceState.Available,
            _time);

        device.Name.Length.ShouldBe(Device.NameMaxLength);
        device.Brand.Length.ShouldBe(Device.BrandMaxLength);
    }

    [Fact]
    public void Create_rejects_name_longer_than_max()
    {
        Should.Throw<DomainValidationException>(() => Device.Create(
            new string('n', Device.NameMaxLength + 1), "Apple", DeviceState.Available, _time));
    }

    [Fact]
    public void Create_rejects_brand_longer_than_max()
    {
        Should.Throw<DomainValidationException>(() => Device.Create(
            "iPhone 15", new string('b', Device.BrandMaxLength + 1), DeviceState.Available, _time));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Create_rejects_undefined_state(int state)
    {
        Should.Throw<DomainValidationException>(
            () => Device.Create("iPhone 15", "Apple", (DeviceState)state, _time));
    }

    // ---------- Update ----------

    [Fact]
    public void Update_changes_fields_and_UpdatedAtUtc_but_keeps_CreatedAtUtc()
    {
        var device = CreateDevice();
        _time.Advance(TimeSpan.FromHours(1));

        device.Update("Galaxy S24", "Samsung", DeviceState.Inactive, _time);

        device.Name.ShouldBe("Galaxy S24");
        device.Brand.ShouldBe("Samsung");
        device.State.ShouldBe(DeviceState.Inactive);
        device.CreatedAtUtc.ShouldBe(CreatedAt.UtcDateTime);
        device.UpdatedAtUtc.ShouldBe(CreatedAt.AddHours(1).UtcDateTime);
    }

    [Fact]
    public void Update_rejects_invalid_input()
    {
        var device = CreateDevice();

        Should.Throw<DomainValidationException>(
            () => device.Update(" ", "Apple", DeviceState.Available, _time));
    }

    [Fact]
    public void Update_rejects_name_change_while_in_use()
    {
        var device = CreateDevice(DeviceState.InUse);

        Should.Throw<BusinessRuleViolationException>(
            () => device.Update("iPhone 16", "Apple", DeviceState.InUse, _time));
    }

    [Fact]
    public void Update_rejects_brand_change_while_in_use()
    {
        var device = CreateDevice(DeviceState.InUse);

        Should.Throw<BusinessRuleViolationException>(
            () => device.Update("iPhone 15", "Samsung", DeviceState.InUse, _time));
    }

    [Fact]
    public void Update_rejects_name_change_while_in_use_even_if_releasing_in_same_call()
    {
        // The rule is checked against the current state, not the requested one.
        var device = CreateDevice(DeviceState.InUse);

        Should.Throw<BusinessRuleViolationException>(
            () => device.Update("iPhone 16", "Apple", DeviceState.Available, _time));
    }

    [Fact]
    public void Update_allows_state_change_while_in_use_when_name_and_brand_are_unchanged()
    {
        var device = CreateDevice(DeviceState.InUse);

        device.Update("iPhone 15", "Apple", DeviceState.Available, _time);

        device.State.ShouldBe(DeviceState.Available);
    }

    [Fact]
    public void Update_compares_name_and_brand_after_trimming()
    {
        var device = CreateDevice(DeviceState.InUse);

        device.Update(" iPhone 15 ", " Apple ", DeviceState.Inactive, _time);

        device.State.ShouldBe(DeviceState.Inactive);
    }

    [Fact]
    public void Failed_update_leaves_device_unchanged()
    {
        var device = CreateDevice(DeviceState.InUse);
        _time.Advance(TimeSpan.FromHours(1));

        Should.Throw<BusinessRuleViolationException>(
            () => device.Update("iPhone 16", "Apple", DeviceState.Available, _time));

        device.Name.ShouldBe("iPhone 15");
        device.State.ShouldBe(DeviceState.InUse);
        device.UpdatedAtUtc.ShouldBe(CreatedAt.UtcDateTime);
    }

    // ---------- Delete ----------

    [Theory]
    [InlineData(DeviceState.Available)]
    [InlineData(DeviceState.Inactive)]
    public void EnsureCanBeDeleted_allows_devices_not_in_use(DeviceState state)
    {
        var device = CreateDevice(state);

        Should.NotThrow(device.EnsureCanBeDeleted);
    }

    [Fact]
    public void EnsureCanBeDeleted_rejects_device_in_use()
    {
        var device = CreateDevice(DeviceState.InUse);

        Should.Throw<BusinessRuleViolationException>(device.EnsureCanBeDeleted);
    }
}
