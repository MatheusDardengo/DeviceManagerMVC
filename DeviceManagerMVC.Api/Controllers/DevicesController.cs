using DeviceManagerMVC.Application.Dtos;
using DeviceManagerMVC.Application.Services;
using DeviceManagerMVC.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeviceManagerMVC.Api.Controllers;

/// <summary>
/// HTTP adapter only: binds the request, calls <see cref="DeviceService"/>, picks the status code.
/// Errors are thrown by the domain/application and turned into ProblemDetails by the exception handler.
/// </summary>
[ApiController]
[Authorize]
[Route("api/devices")]
public sealed class DevicesController(DeviceService deviceService) : ControllerBase
{
    /// <summary>
    /// Lists all devices ordered by id, optionally filtered by exact brand and/or state.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeviceResponse>>> List(
        [FromQuery] string? brand, [FromQuery] DeviceState? state, CancellationToken cancellationToken) =>
        Ok(await deviceService.ListAsync(brand, state, cancellationToken));

    /// <summary>
    /// Gets a device by id. Returns 404 when it does not exist.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<DeviceResponse>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await deviceService.GetByIdAsync(id, cancellationToken));

    /// <summary>
    /// Creates a device. Returns 201 with a Location header pointing to the new device.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<DeviceResponse>> Create(CreateDeviceRequest request, CancellationToken cancellationToken)
    {
        var device = await deviceService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = device.Id }, device);
    }

    /// <summary>
    /// Replaces a device (all fields required). Name and brand cannot change while the device is in use (409).
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<DeviceResponse>> Update(int id, UpdateDeviceRequest request, CancellationToken cancellationToken) =>
        Ok(await deviceService.UpdateAsync(id, request, cancellationToken));

    /// <summary>
    /// Partially updates a device: omitted or null fields keep their value.
    /// Name and brand cannot change while the device is in use (409).
    /// </summary>
    [HttpPatch("{id:int}")]
    public async Task<ActionResult<DeviceResponse>> Patch(int id, PatchDeviceRequest request, CancellationToken cancellationToken) =>
        Ok(await deviceService.PatchAsync(id, request, cancellationToken));

    /// <summary>
    /// Deletes a device. A device in use cannot be deleted (409).
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await deviceService.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}
