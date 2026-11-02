using DeviceManagerMVC.Application.Common;
using DeviceManagerMVC.Application.Dtos;
using DeviceManagerMVC.Application.Services;
using DeviceManagerMVC.Domain.Common;
using DeviceManagerMVC.Domain.Entities;
using DeviceManagerMVC.Domain.Enums;
using DeviceManagerMVC.Domain.Repositories;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace DeviceManagerMVC.UnitTests.Application.Services;

public sealed class DeviceServiceTests
{
    private const int DeviceId = 7;
    private const int UnknownId = 999;

    private static readonly DateTimeOffset Now = new(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);

    private readonly IDeviceRepository _repository = Substitute.For<IDeviceRepository>();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly CancellationToken _ct = new CancellationTokenSource().Token;
    private readonly DeviceService _sut;

    public DeviceServiceTests()
    {
        _sut = new DeviceService(_repository, _time);
        _repository.GetByIdAsync(UnknownId, Arg.Any<CancellationToken>()).Returns((Device?)null);
    }

    private Device GivenStoredDevice(DeviceState state = DeviceState.Available)
    {
        var device = Device.Create("iPhone 15", "Apple", state, _time);
        _repository.GetByIdAsync(DeviceId, _ct).Returns(device);
        _repository.UpdateAsync(DeviceId, device, _ct).Returns(true);
        _repository.DeleteAsync(DeviceId, _ct).Returns(true);
        return device;
    }

    /// <summary>The device is found, but a concurrent request deletes it before we write.</summary>
    private void GivenDeviceDeletedConcurrently(Device device)
    {
        _repository.UpdateAsync(DeviceId, device, _ct).Returns(false);
        _repository.DeleteAsync(DeviceId, _ct).Returns(false);
    }

    // ---------- Create ----------

    [Fact]
    public async Task Create_persists_device_and_returns_it_with_generated_id()
    {
        _repository.AddAsync(Arg.Any<Device>(), _ct).Returns(42);

        var response = await _sut.CreateAsync(new CreateDeviceRequest("iPhone 15", "Apple", DeviceState.Available), _ct);

        response.ShouldBe(new DeviceResponse(42, "iPhone 15", "Apple", DeviceState.Available, Now.UtcDateTime, Now.UtcDateTime));
        await _repository.Received(1).AddAsync(
            Arg.Is<Device>(d => d.Name == "iPhone 15" && d.Brand == "Apple" && d.State == DeviceState.Available), _ct);
    }

    [Fact]
    public async Task Create_with_invalid_input_throws_and_does_not_persist()
    {
        await Should.ThrowAsync<DomainValidationException>(
            () => _sut.CreateAsync(new CreateDeviceRequest(" ", "Apple", DeviceState.Available), _ct));

        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    // ---------- Get ----------

    [Fact]
    public async Task GetById_returns_device()
    {
        GivenStoredDevice(DeviceState.InUse);

        var response = await _sut.GetByIdAsync(DeviceId, _ct);

        response.ShouldBe(new DeviceResponse(DeviceId, "iPhone 15", "Apple", DeviceState.InUse, Now.UtcDateTime, Now.UtcDateTime));
    }

    [Fact]
    public async Task GetById_unknown_throws_NotFound()
    {
        await Should.ThrowAsync<NotFoundException>(() => _sut.GetByIdAsync(UnknownId, _ct));
    }

    // ---------- List ----------

    [Fact]
    public async Task List_forwards_filters_and_maps_results()
    {
        IReadOnlyList<Device> stored =
        [
            Device.Create("iPhone 15", "Apple", DeviceState.InUse, _time),
            Device.Create("MacBook Pro 14", "Apple", DeviceState.InUse, _time)
        ];
        _repository.ListAsync("Apple", DeviceState.InUse, _ct).Returns(stored);

        var response = await _sut.ListAsync("Apple", DeviceState.InUse, _ct);

        response.Select(d => d.Name).ShouldBe(["iPhone 15", "MacBook Pro 14"]);
        response.ShouldAllBe(d => d.Brand == "Apple" && d.State == DeviceState.InUse);
    }

    // ---------- Update (PUT) ----------

    [Fact]
    public async Task Update_applies_changes_persists_and_returns_device()
    {
        var device = GivenStoredDevice();
        _time.Advance(TimeSpan.FromHours(1));

        var response = await _sut.UpdateAsync(
            DeviceId, new UpdateDeviceRequest("Galaxy S24", "Samsung", DeviceState.Inactive), _ct);

        response.ShouldBe(new DeviceResponse(
            DeviceId, "Galaxy S24", "Samsung", DeviceState.Inactive, Now.UtcDateTime, Now.AddHours(1).UtcDateTime));
        await _repository.Received(1).UpdateAsync(DeviceId, device, _ct);
    }

    [Fact]
    public async Task Update_unknown_throws_NotFound()
    {
        await Should.ThrowAsync<NotFoundException>(() => _sut.UpdateAsync(
            UnknownId, new UpdateDeviceRequest("Galaxy S24", "Samsung", DeviceState.Available), _ct));

        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default, default!, default);
    }

    [Fact]
    public async Task Update_renaming_device_in_use_throws_and_does_not_persist()
    {
        GivenStoredDevice(DeviceState.InUse);

        await Should.ThrowAsync<BusinessRuleViolationException>(() => _sut.UpdateAsync(
            DeviceId, new UpdateDeviceRequest("iPhone 16", "Apple", DeviceState.InUse), _ct));

        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default, default!, default);
    }

    [Fact]
    public async Task Update_throws_NotFound_when_device_was_deleted_concurrently()
    {
        GivenDeviceDeletedConcurrently(GivenStoredDevice());

        await Should.ThrowAsync<NotFoundException>(() => _sut.UpdateAsync(
            DeviceId, new UpdateDeviceRequest("Galaxy S24", "Samsung", DeviceState.Available), _ct));
    }

    // ---------- Patch ----------

    [Fact]
    public async Task Patch_throws_NotFound_when_device_was_deleted_concurrently()
    {
        GivenDeviceDeletedConcurrently(GivenStoredDevice());

        await Should.ThrowAsync<NotFoundException>(
            () => _sut.PatchAsync(DeviceId, new PatchDeviceRequest(null, null, DeviceState.Inactive), _ct));
    }

    [Fact]
    public async Task Patch_changes_only_provided_fields()
    {
        var device = GivenStoredDevice(DeviceState.InUse);

        var response = await _sut.PatchAsync(DeviceId, new PatchDeviceRequest(null, null, DeviceState.Available), _ct);

        response.Name.ShouldBe("iPhone 15");
        response.Brand.ShouldBe("Apple");
        response.State.ShouldBe(DeviceState.Available);
        await _repository.Received(1).UpdateAsync(DeviceId, device, _ct);
    }

    [Fact]
    public async Task Patch_name_only_keeps_brand_and_state()
    {
        GivenStoredDevice(DeviceState.Inactive);

        var response = await _sut.PatchAsync(DeviceId, new PatchDeviceRequest("iPhone 15 Pro", null, null), _ct);

        response.Name.ShouldBe("iPhone 15 Pro");
        response.Brand.ShouldBe("Apple");
        response.State.ShouldBe(DeviceState.Inactive);
    }

    [Fact]
    public async Task Patch_unknown_throws_NotFound()
    {
        await Should.ThrowAsync<NotFoundException>(
            () => _sut.PatchAsync(UnknownId, new PatchDeviceRequest("x", null, null), _ct));
    }

    [Fact]
    public async Task Patch_renaming_device_in_use_throws_and_does_not_persist()
    {
        GivenStoredDevice(DeviceState.InUse);

        await Should.ThrowAsync<BusinessRuleViolationException>(
            () => _sut.PatchAsync(DeviceId, new PatchDeviceRequest("iPhone 16", null, null), _ct));

        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default, default!, default);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_removes_device()
    {
        GivenStoredDevice(DeviceState.Available);

        await _sut.DeleteAsync(DeviceId, _ct);

        await _repository.Received(1).DeleteAsync(DeviceId, _ct);
    }

    [Fact]
    public async Task Delete_throws_NotFound_when_device_was_deleted_concurrently()
    {
        GivenDeviceDeletedConcurrently(GivenStoredDevice());

        await Should.ThrowAsync<NotFoundException>(() => _sut.DeleteAsync(DeviceId, _ct));
    }

    [Fact]
    public async Task Delete_unknown_throws_NotFound()
    {
        await Should.ThrowAsync<NotFoundException>(() => _sut.DeleteAsync(UnknownId, _ct));

        await _repository.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }

    [Fact]
    public async Task Delete_device_in_use_throws_and_does_not_delete()
    {
        GivenStoredDevice(DeviceState.InUse);

        await Should.ThrowAsync<BusinessRuleViolationException>(() => _sut.DeleteAsync(DeviceId, _ct));

        await _repository.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }
}
