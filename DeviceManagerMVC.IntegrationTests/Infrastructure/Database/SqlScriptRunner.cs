using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace DeviceManagerMVC.IntegrationTests.Infrastructure.Database;

/// <summary>
/// Runs sqlcmd-style scripts through ADO.NET. "GO" is a sqlcmd separator, not T-SQL,
/// so each script is split on GO lines and every batch is executed on one connection
/// (keeps USE [DeviceManager] in effect for the following batches).
/// </summary>
internal static partial class SqlScriptRunner
{
    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex GoSeparator();

    public static async Task RunAsync(
        string connectionString, IEnumerable<string> scriptPaths, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var path in scriptPaths)
        {
            var script = await File.ReadAllTextAsync(path, cancellationToken);

            foreach (var batch in GoSeparator().Split(script).Where(b => !string.IsNullOrWhiteSpace(b)))
            {
                await using var command = new SqlCommand(batch, connection);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }
}
