using Microsoft.Data.SqlClient;
using Shouldly;

namespace DeviceManagerMVC.IntegrationTests.Infrastructure.Database;

[Collection(DatabaseCollection.Name)]
public sealed class SchemaSmokeTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Schema_scripts_create_expected_tables()
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo');", connection);

        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        tables.ShouldBe(["Users", "RefreshTokens", "Devices"], ignoreOrder: true);
    }
}
