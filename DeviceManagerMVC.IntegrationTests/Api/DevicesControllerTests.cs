using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeviceManagerMVC.Application.Dtos;
using DeviceManagerMVC.Domain.Entities;
using DeviceManagerMVC.Domain.Enums;
using DeviceManagerMVC.Domain.Repositories;
using DeviceManagerMVC.IntegrationTests.Infrastructure.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace DeviceManagerMVC.IntegrationTests.Api;

[Collection(DatabaseCollection.Name)]
public sealed class DevicesControllerTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string Devices = "/api/devices";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly DeviceManagerApiFactory _factory = new(fixture.ConnectionString);
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetDevicesAsync();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task<DeviceResponse> GivenDeviceAsync(
        string name = "iPhone 15", string brand = "Apple", DeviceState state = DeviceState.Available)
    {
        var response = await _client.PostAsJsonAsync(Devices, new CreateDeviceRequest(name, brand, state), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DeviceResponse>(Json))!;
    }

    private static async Task<ProblemDetails> ShouldBeProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json);
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe((int)status);
        return problem;
    }

    // ---------- POST ----------

    [Fact]
    public async Task Post_creates_device_and_returns_201_with_location()
    {
        var response = await _client.PostAsJsonAsync(
            Devices, new CreateDeviceRequest("iPhone 15", "Apple", DeviceState.InUse), Json);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<DeviceResponse>(Json);
        body.ShouldNotBeNull();
        body.Id.ShouldBeGreaterThan(0);
        body.Name.ShouldBe("iPhone 15");
        body.Brand.ShouldBe("Apple");
        body.State.ShouldBe(DeviceState.InUse);
        response.Headers.Location!.ToString().ShouldEndWith($"{Devices}/{body.Id}");
    }

    [Fact]
    public async Task Post_serializes_state_as_string()
    {
        var response = await _client.PostAsJsonAsync(
            Devices, new CreateDeviceRequest("iPhone 15", "Apple", DeviceState.InUse), Json);

        (await response.Content.ReadAsStringAsync()).ShouldContain("\"state\":\"InUse\"");
    }

    [Fact]
    public async Task Post_with_blank_name_returns_400_problem()
    {
        var response = await _client.PostAsJsonAsync(
            Devices, new CreateDeviceRequest("  ", "Apple", DeviceState.Available), Json);

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_with_name_longer_than_max_returns_400_problem()
    {
        var response = await _client.PostAsJsonAsync(
            Devices, new CreateDeviceRequest(new string('n', Device.NameMaxLength + 1), "Apple", DeviceState.Available), Json);

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_with_undefined_numeric_state_returns_400_problem()
    {
        using var content = new StringContent(
            """{"name":"iPhone 15","brand":"Apple","state":4}""", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(Devices, content);

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_with_unknown_state_name_returns_400_problem()
    {
        using var content = new StringContent(
            """{"name":"iPhone 15","brand":"Apple","state":"Broken"}""", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(Devices, content);

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest);
    }

    // ---------- GET ----------

    [Fact]
    public async Task Get_by_id_returns_200_with_device()
    {
        var created = await GivenDeviceAsync();

        var response = await _client.GetAsync($"{Devices}/{created.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<DeviceResponse>(Json)).ShouldBe(created);
    }

    [Fact]
    public async Task Get_by_unknown_id_returns_404_problem()
    {
        var response = await _client.GetAsync($"{Devices}/{int.MaxValue}");

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_returns_all_devices()
    {
        await GivenDeviceAsync("iPhone 15", "Apple");
        await GivenDeviceAsync("Galaxy S24", "Samsung");

        var devices = await _client.GetFromJsonAsync<List<DeviceResponse>>(Devices, Json);

        devices!.Select(d => d.Name).ShouldBe(["iPhone 15", "Galaxy S24"]);
    }

    [Fact]
    public async Task List_filters_by_brand_and_state_from_query_string()
    {
        await GivenDeviceAsync("iPhone 15", "Apple", DeviceState.InUse);
        await GivenDeviceAsync("MacBook Pro 14", "Apple", DeviceState.Available);
        await GivenDeviceAsync("Galaxy S24", "Samsung", DeviceState.InUse);

        var devices = await _client.GetFromJsonAsync<List<DeviceResponse>>($"{Devices}?brand=Apple&state=InUse", Json);

        devices!.Select(d => d.Name).ShouldBe(["iPhone 15"]);
    }

    // ---------- PUT ----------

    [Fact]
    public async Task Put_updates_device_and_returns_200()
    {
        var created = await GivenDeviceAsync();

        var response = await _client.PutAsJsonAsync(
            $"{Devices}/{created.Id}", new UpdateDeviceRequest("Galaxy S24", "Samsung", DeviceState.Inactive), Json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DeviceResponse>(Json);
        body!.Name.ShouldBe("Galaxy S24");
        body.Brand.ShouldBe("Samsung");
        body.State.ShouldBe(DeviceState.Inactive);
        body.CreatedAtUtc.ShouldBe(created.CreatedAtUtc);
    }

    [Fact]
    public async Task Put_renaming_device_in_use_returns_409_problem()
    {
        var created = await GivenDeviceAsync(state: DeviceState.InUse);

        var response = await _client.PutAsJsonAsync(
            $"{Devices}/{created.Id}", new UpdateDeviceRequest("iPhone 16", "Apple", DeviceState.InUse), Json);

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Put_unknown_id_returns_404_problem()
    {
        var response = await _client.PutAsJsonAsync(
            $"{Devices}/{int.MaxValue}", new UpdateDeviceRequest("x", "y", DeviceState.Available), Json);

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound);
    }

    // ---------- PATCH ----------

    [Fact]
    public async Task Patch_changes_only_provided_fields_and_returns_200()
    {
        var created = await GivenDeviceAsync(state: DeviceState.InUse);

        var response = await _client.PatchAsJsonAsync(
            $"{Devices}/{created.Id}", new PatchDeviceRequest(null, null, DeviceState.Available), Json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DeviceResponse>(Json);
        body!.Name.ShouldBe("iPhone 15");
        body.Brand.ShouldBe("Apple");
        body.State.ShouldBe(DeviceState.Available);
    }

    [Fact]
    public async Task Patch_renaming_device_in_use_returns_409_problem()
    {
        var created = await GivenDeviceAsync(state: DeviceState.InUse);

        var response = await _client.PatchAsJsonAsync(
            $"{Devices}/{created.Id}", new PatchDeviceRequest("iPhone 16", null, null), Json);

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict);
    }

    // ---------- DELETE ----------

    [Fact]
    public async Task Delete_returns_204_and_device_is_gone()
    {
        var created = await GivenDeviceAsync();

        var response = await _client.DeleteAsync($"{Devices}/{created.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _client.GetAsync($"{Devices}/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_device_in_use_returns_409_problem()
    {
        var created = await GivenDeviceAsync(state: DeviceState.InUse);

        var response = await _client.DeleteAsync($"{Devices}/{created.Id}");

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_unknown_id_returns_404_problem()
    {
        var response = await _client.DeleteAsync($"{Devices}/{int.MaxValue}");

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound);
    }

    // ---------- Unexpected errors ----------

    [Fact]
    public async Task Unexpected_exception_returns_500_problem_without_details()
    {
        var repository = Substitute.For<IDeviceRepository>();
        repository.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Sensitive internal detail"));

        await using var factory = new DeviceManagerApiFactory(fixture.ConnectionString)
        {
            ConfigureServices = services => services.AddScoped(_ => repository)
        };
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"{Devices}/1");

        await ShouldBeProblemAsync(response, HttpStatusCode.InternalServerError);
        (await response.Content.ReadAsStringAsync()).ShouldNotContain("Sensitive");
    }
}
