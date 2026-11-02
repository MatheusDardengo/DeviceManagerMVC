using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace DeviceManagerMVC.IntegrationTests.Infrastructure.Database;

/// <summary>
/// Starts one SQL Server 2022 container per test run and applies the same
/// database/*.sql scripts docker-compose uses. The seed is skipped on purpose:
/// tests arrange their own data.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string DatabaseName = "DeviceManager";

    private static readonly string[] SchemaScripts =
    [
        "01_create_database.sql",
        "02_create_tables.sql"
    ];

    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    /// <summary>Connection string pointing at the DeviceManager database.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var scriptsDirectory = Path.Combine(AppContext.BaseDirectory, "database");
        await SqlScriptRunner.RunAsync(
            _container.GetConnectionString(),
            SchemaScripts.Select(script => Path.Combine(scriptsDirectory, script)));

        ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = DatabaseName
        }.ConnectionString;
    }

    /// <summary>
    /// Empties the Devices table so each test starts clean. Identity is not reseeded:
    /// tests use the ids returned by the repository, never hardcoded ones.
    /// </summary>
    public Task ResetDevicesAsync() => ExecuteAsync("DELETE FROM dbo.Devices;");

    /// <summary>Empties Users; RefreshTokens go with them through ON DELETE CASCADE.</summary>
    public Task ResetUsersAsync() => ExecuteAsync("DELETE FROM dbo.Users;");

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
