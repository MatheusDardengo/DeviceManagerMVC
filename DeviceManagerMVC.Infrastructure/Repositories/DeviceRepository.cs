using System.Data;
using DeviceManagerMVC.Domain.Entities;
using DeviceManagerMVC.Domain.Enums;
using DeviceManagerMVC.Domain.Repositories;
using DeviceManagerMVC.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;

namespace DeviceManagerMVC.Infrastructure.Repositories;

/// <summary>
/// ADO.NET persistence for <see cref="Device"/>. Every value goes through a typed SqlParameter;
/// no business rules here, only mapping between rows and the entity.
/// </summary>
public sealed class DeviceRepository(SqlConnectionFactory connectionFactory) : IDeviceRepository
{
    private const string SelectColumns = "SELECT Id, Name, Brand, State, CreatedAtUtc, UpdatedAtUtc FROM dbo.Devices";

    public async Task<Device?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand($"{SelectColumns} WHERE Id = @Id;", connection);
        AddId(command, id);

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<IReadOnlyList<Device>> ListAsync(string? brand, DeviceState? state, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        // Only fixed SQL fragments are appended; the filter values are always parameters.
        var conditions = new List<string>();
        if (brand is not null)
        {
            conditions.Add("Brand = @Brand");
            AddBrand(command, brand);
        }

        if (state is not null)
        {
            conditions.Add("State = @State");
            AddState(command, state.Value);
        }

        var where = conditions.Count > 0 ? $" WHERE {string.Join(" AND ", conditions)}" : string.Empty;
        command.CommandText = $"{SelectColumns}{where} ORDER BY Id;";

        var devices = new List<Device>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            devices.Add(Map(reader));
        }

        return devices;
    }

    public async Task<int> AddAsync(Device device, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.Devices (Name, Brand, State, CreatedAtUtc, UpdatedAtUtc)
            OUTPUT INSERTED.Id
            VALUES (@Name, @Brand, @State, @CreatedAtUtc, @UpdatedAtUtc);
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        AddName(command, device.Name);
        AddBrand(command, device.Brand);
        AddState(command, device.State);
        AddDateTime(command, "@CreatedAtUtc", device.CreatedAtUtc);
        AddDateTime(command, "@UpdatedAtUtc", device.UpdatedAtUtc);

        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task UpdateAsync(int id, Device device, CancellationToken cancellationToken)
    {
        // CreatedAtUtc is deliberately absent: it never changes after creation.
        const string sql = """
            UPDATE dbo.Devices
            SET Name = @Name, Brand = @Brand, State = @State, UpdatedAtUtc = @UpdatedAtUtc
            WHERE Id = @Id;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        AddId(command, id);
        AddName(command, device.Name);
        AddBrand(command, device.Brand);
        AddState(command, device.State);
        AddDateTime(command, "@UpdatedAtUtc", device.UpdatedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("DELETE FROM dbo.Devices WHERE Id = @Id;", connection);
        AddId(command, id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Device Map(SqlDataReader reader) =>
        Device.Rehydrate(
            id: reader.GetInt32(0),
            name: reader.GetString(1),
            brand: reader.GetString(2),
            state: (DeviceState)reader.GetByte(3),
            createdAtUtc: reader.GetDateTime(4),
            updatedAtUtc: reader.GetDateTime(5));

    // Explicit types and sizes: no type inference surprises and one cached plan per query
    // (unsized NVARCHAR parameters get a different plan per string length).
    private static void AddId(SqlCommand command, int id) =>
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

    private static void AddName(SqlCommand command, string name) =>
        command.Parameters.Add("@Name", SqlDbType.NVarChar, Device.NameMaxLength).Value = name;

    private static void AddBrand(SqlCommand command, string brand) =>
        command.Parameters.Add("@Brand", SqlDbType.NVarChar, Device.BrandMaxLength).Value = brand;

    private static void AddState(SqlCommand command, DeviceState state) =>
        command.Parameters.Add("@State", SqlDbType.TinyInt).Value = (byte)state;

    private static void AddDateTime(SqlCommand command, string name, DateTime value) =>
        command.Parameters.Add(name, SqlDbType.DateTime2).Value = value;
}
