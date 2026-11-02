using Microsoft.Data.SqlClient;

namespace DeviceManagerMVC.Infrastructure.Persistence;

/// <summary>
/// Hands out opened connections. One connection per operation is cheap because
/// SqlClient pools the physical connections behind the scenes.
/// </summary>
public sealed class SqlConnectionFactory(string connectionString)
{
    public async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
