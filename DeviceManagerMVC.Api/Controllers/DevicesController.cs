using DeviceManagerMVC.Application.Dtos;
using DeviceManagerMVC.Application.Services;
using DeviceManagerMVC.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace DeviceManagerMVC.Api.Controllers;

[ApiController]
[Route("api/devices")]
[Produces("application/json")]
public sealed class DevicesController(DeviceService deviceService) : ControllerBase
{
    [HttpGet]
    public Task<ActionResult<IReadOnlyList<DeviceResponse>>> List(
        [FromQuery] string? brand, [FromQuery] DeviceState? state, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    [HttpGet("{id:int}")]
    public Task<ActionResult<DeviceResponse>> GetById(int id, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    [HttpPost]
    public Task<ActionResult<DeviceResponse>> Create(CreateDeviceRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    [HttpPut("{id:int}")]
    public Task<ActionResult<DeviceResponse>> Update(int id, UpdateDeviceRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    [HttpPatch("{id:int}")]
    public Task<ActionResult<DeviceResponse>> Patch(int id, PatchDeviceRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    [HttpDelete("{id:int}")]
    public Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
