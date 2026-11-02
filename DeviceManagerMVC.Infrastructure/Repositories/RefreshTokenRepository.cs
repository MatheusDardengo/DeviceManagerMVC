using System.Data;
using DeviceManagerMVC.Domain.Entities;
using DeviceManagerMVC.Domain.Repositories;
using DeviceManagerMVC.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;

namespace DeviceManagerMVC.Infrastructure.Repositories;

/// <summary>ADO.NET persistence for <see cref="RefreshToken"/>. Tokens are looked up by their SHA-256 hash.</summary>
public sealed class RefreshTokenRepository(SqlConnectionFactory connectionFactory) : IRefreshTokenRepository
{
    public async Task<RefreshToken?> GetByHashAsync(byte[] tokenHash, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT UserId, TokenHash, FamilyId, CreatedAtUtc, ExpiresAtUtc, RevokedAtUtc, ReplacedByTokenHash
            FROM dbo.RefreshTokens
            WHERE TokenHash = @TokenHash;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        AddHash(command, "@TokenHash", tokenHash);

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        await InsertAsync(connection, transaction: null, token, cancellationToken);
    }

    public async Task<bool> RotateAsync(RefreshToken current, RefreshToken replacement, CancellationToken cancellationToken)
    {
        var revokedAtUtc = current.RevokedAtUtc
            ?? throw new InvalidOperationException("Rotate the token in the domain before persisting the rotation.");

        // "AND RevokedAtUtc IS NULL" makes the update a compare-and-set: of two requests rotating
        // the same token, the second one waits on the row lock and then matches zero rows.
        const string sql = """
            UPDATE dbo.RefreshTokens
            SET RevokedAtUtc = @RevokedAtUtc, ReplacedByTokenHash = @ReplacedByTokenHash
            WHERE TokenHash = @TokenHash AND RevokedAtUtc IS NULL;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = new SqlCommand(sql, connection, transaction))
        {
            AddHash(command, "@TokenHash", current.TokenHash);
            AddNullableHash(command, "@ReplacedByTokenHash", current.ReplacedByTokenHash);
            AddDateTime(command, "@RevokedAtUtc", revokedAtUtc);

            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }
        }

        await InsertAsync(connection, transaction, replacement, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    public async Task RevokeFamilyAsync(Guid familyId, DateTime revokedAtUtc, CancellationToken cancellationToken)
    {
        // Already-revoked tokens keep their original time: it records when each one was used.
        const string sql = """
            UPDATE dbo.RefreshTokens
            SET RevokedAtUtc = @RevokedAtUtc
            WHERE FamilyId = @FamilyId AND RevokedAtUtc IS NULL;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@FamilyId", SqlDbType.UniqueIdentifier).Value = familyId;
        AddDateTime(command, "@RevokedAtUtc", revokedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAsync(
        SqlConnection connection, SqlTransaction? transaction, RefreshToken token, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.RefreshTokens
                (UserId, TokenHash, FamilyId, CreatedAtUtc, ExpiresAtUtc, RevokedAtUtc, ReplacedByTokenHash)
            VALUES
                (@UserId, @TokenHash, @FamilyId, @CreatedAtUtc, @ExpiresAtUtc, @RevokedAtUtc, @ReplacedByTokenHash);
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = token.UserId;
        AddHash(command, "@TokenHash", token.TokenHash);
        command.Parameters.Add("@FamilyId", SqlDbType.UniqueIdentifier).Value = token.FamilyId;
        AddDateTime(command, "@CreatedAtUtc", token.CreatedAtUtc);
        AddDateTime(command, "@ExpiresAtUtc", token.ExpiresAtUtc);
        command.Parameters.Add("@RevokedAtUtc", SqlDbType.DateTime2).Value = (object?)token.RevokedAtUtc ?? DBNull.Value;
        AddNullableHash(command, "@ReplacedByTokenHash", token.ReplacedByTokenHash);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static RefreshToken Map(SqlDataReader reader) =>
        RefreshToken.Rehydrate(
            userId: reader.GetInt32(0),
            tokenHash: reader.GetFieldValue<byte[]>(1),
            familyId: reader.GetGuid(2),
            createdAtUtc: reader.GetDateTime(3),
            expiresAtUtc: reader.GetDateTime(4),
            revokedAtUtc: reader.IsDBNull(5) ? null : reader.GetDateTime(5),
            replacedByTokenHash: reader.IsDBNull(6) ? null : reader.GetFieldValue<byte[]>(6));

    private static void AddHash(SqlCommand command, string name, byte[] hash) =>
        command.Parameters.Add(name, SqlDbType.Binary, RefreshToken.TokenHashLength).Value = hash;

    private static void AddNullableHash(SqlCommand command, string name, byte[]? hash) =>
        command.Parameters.Add(name, SqlDbType.Binary, RefreshToken.TokenHashLength).Value = (object?)hash ?? DBNull.Value;

    private static void AddDateTime(SqlCommand command, string name, DateTime value) =>
        command.Parameters.Add(name, SqlDbType.DateTime2).Value = value;
}
