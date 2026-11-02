using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace DeviceManagerMVC.IntegrationTests.Infrastructure.Database;

/// <summary>
/// Starts one SQL Server 2022 container per test run.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string DatabaseName = "DeviceManager";

    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    /// <summary>Connection string pointing at the DeviceManager database.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = DatabaseName
        }.ConnectionString;
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
