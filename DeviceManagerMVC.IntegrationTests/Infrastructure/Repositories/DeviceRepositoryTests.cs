using DeviceManagerMVC.Domain.Entities;
using DeviceManagerMVC.Domain.Enums;
using DeviceManagerMVC.Infrastructure.Persistence;
using DeviceManagerMVC.Infrastructure.Repositories;
using DeviceManagerMVC.IntegrationTests.Infrastructure.Database;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace DeviceManagerMVC.IntegrationTests.Infrastructure.Repositories;

[Collection(DatabaseCollection.Name)]
public sealed class DeviceRepositoryTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Now);
    private readonly DeviceRepository _sut = new(new SqlConnectionFactory(fixture.ConnectionString));
    private readonly CancellationToken _ct = CancellationToken.None;

    public Task InitializeAsync() => fixture.ResetDevicesAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private Device NewDevice(string name = "iPhone 15", string brand = "Apple", DeviceState state = DeviceState.Available) =>
        Device.Create(name, brand, state, _time);

    // ---------- Add / GetById ----------

    [Fact]
    public async Task Add_returns_generated_id_and_persists_every_field()
    {
        var id = await _sut.AddAsync(NewDevice(state: DeviceState.InUse), _ct);

        var stored = await _sut.GetByIdAsync(id, _ct);

        id.ShouldBeGreaterThan(0);
        stored.ShouldNotBeNull();
        stored.Id.ShouldBe(id);
        stored.Name.ShouldBe("iPhone 15");
        stored.Brand.ShouldBe("Apple");
        stored.State.ShouldBe(DeviceState.InUse);
        stored.CreatedAtUtc.ShouldBe(Now.UtcDateTime);
        stored.UpdatedAtUtc.ShouldBe(Now.UtcDateTime);
    }

    [Fact]
    public async Task GetById_returns_timestamps_as_utc()
    {
        var id = await _sut.AddAsync(NewDevice(), _ct);

        var stored = await _sut.GetByIdAsync(id, _ct);

        stored!.CreatedAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
        stored.UpdatedAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task GetById_unknown_returns_null()
    {
        var stored = await _sut.GetByIdAsync(int.MaxValue, _ct);

        stored.ShouldBeNull();
    }

    [Fact]
    public async Task Add_accepts_name_and_brand_at_domain_max_length()
    {
        // Guards against drift between Device.*MaxLength and NVARCHAR(100) in 02_create_tables.sql.
        var device = NewDevice(new string('n', Device.NameMaxLength), new string('b', Device.BrandMaxLength));

        var id = await _sut.AddAsync(device, _ct);

        var stored = await _sut.GetByIdAsync(id, _ct);
        stored!.Name.Length.ShouldBe(Device.NameMaxLength);
        stored.Brand.Length.ShouldBe(Device.BrandMaxLength);
    }

    // ---------- List ----------

    [Fact]
    public async Task List_without_filters_returns_all_devices_ordered_by_id_with_real_ids()
    {
        var first = await _sut.AddAsync(NewDevice("iPhone 15", "Apple"), _ct);
        var second = await _sut.AddAsync(NewDevice("Galaxy S24", "Samsung"), _ct);

        var devices = await _sut.ListAsync(null, null, _ct);

        devices.Select(d => d.Id).ShouldBe([first, second]);
        devices.Select(d => d.Name).ShouldBe(["iPhone 15", "Galaxy S24"]);
    }

    [Fact]
    public async Task List_filters_by_brand()
    {
        await _sut.AddAsync(NewDevice("iPhone 15", "Apple"), _ct);
        await _sut.AddAsync(NewDevice("Galaxy S24", "Samsung"), _ct);

        var devices = await _sut.ListAsync("Samsung", null, _ct);

        devices.Select(d => d.Name).ShouldBe(["Galaxy S24"]);
    }

    [Fact]
    public async Task List_filters_by_state()
    {
        await _sut.AddAsync(NewDevice("iPhone 15", state: DeviceState.InUse), _ct);
        await _sut.AddAsync(NewDevice("MacBook Pro 14", state: DeviceState.Available), _ct);

        var devices = await _sut.ListAsync(null, DeviceState.InUse, _ct);

        devices.Select(d => d.Name).ShouldBe(["iPhone 15"]);
    }

    [Fact]
    public async Task List_combines_brand_and_state_filters()
    {
        await _sut.AddAsync(NewDevice("iPhone 15", "Apple", DeviceState.InUse), _ct);
        await _sut.AddAsync(NewDevice("MacBook Pro 14", "Apple", DeviceState.Available), _ct);
        await _sut.AddAsync(NewDevice("Galaxy S24", "Samsung", DeviceState.InUse), _ct);

        var devices = await _sut.ListAsync("Apple", DeviceState.InUse, _ct);

        devices.Select(d => d.Name).ShouldBe(["iPhone 15"]);
    }

    [Fact]
    public async Task List_returns_empty_when_nothing_matches()
    {
        await _sut.AddAsync(NewDevice("iPhone 15", "Apple"), _ct);

        var devices = await _sut.ListAsync("Nokia", null, _ct);

        devices.ShouldBeEmpty();
    }

    // ---------- Update ----------

    [Fact]
    public async Task Update_persists_changes_and_keeps_CreatedAtUtc()
    {
        var id = await _sut.AddAsync(NewDevice(), _ct);
        var device = await _sut.GetByIdAsync(id, _ct);
        _time.Advance(TimeSpan.FromHours(1));
        device!.Update("Galaxy S24", "Samsung", DeviceState.Inactive, _time);

        await _sut.UpdateAsync(id, device, _ct);

        var stored = await _sut.GetByIdAsync(id, _ct);
        stored!.Name.ShouldBe("Galaxy S24");
        stored.Brand.ShouldBe("Samsung");
        stored.State.ShouldBe(DeviceState.Inactive);
        stored.CreatedAtUtc.ShouldBe(Now.UtcDateTime);
        stored.UpdatedAtUtc.ShouldBe(Now.AddHours(1).UtcDateTime);
    }

    [Fact]
    public async Task Update_only_touches_the_given_device()
    {
        var target = await _sut.AddAsync(NewDevice("iPhone 15"), _ct);
        var other = await _sut.AddAsync(NewDevice("Galaxy S24", "Samsung"), _ct);
        var device = await _sut.GetByIdAsync(target, _ct);
        device!.Update("iPhone 16", "Apple", DeviceState.Available, _time);

        await _sut.UpdateAsync(target, device, _ct);

        (await _sut.GetByIdAsync(other, _ct))!.Name.ShouldBe("Galaxy S24");
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_removes_only_the_given_device()
    {
        var target = await _sut.AddAsync(NewDevice("iPhone 15"), _ct);
        var other = await _sut.AddAsync(NewDevice("Galaxy S24", "Samsung"), _ct);

        await _sut.DeleteAsync(target, _ct);

        (await _sut.GetByIdAsync(target, _ct)).ShouldBeNull();
        (await _sut.GetByIdAsync(other, _ct)).ShouldNotBeNull();
    }
}
