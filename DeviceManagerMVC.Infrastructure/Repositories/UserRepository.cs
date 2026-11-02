using System.Data;
using DeviceManagerMVC.Domain.Common;
using DeviceManagerMVC.Domain.Entities;
using DeviceManagerMVC.Domain.Repositories;
using DeviceManagerMVC.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;

namespace DeviceManagerMVC.Infrastructure.Repositories;

/// <summary>ADO.NET persistence for <see cref="User"/>.</summary>
public sealed class UserRepository(SqlConnectionFactory connectionFactory) : IUserRepository
{
    private const string SelectColumns = "SELECT Id, Email, PasswordHash, CreatedAtUtc FROM dbo.Users";

    private const int PasswordHashMaxLength = 512; // NVARCHAR(512)

    // SQL Server error numbers for a duplicate key on a UNIQUE constraint / unique index.
    private const int UniqueConstraintViolation = 2627;
    private const int UniqueIndexViolation = 2601;

    public async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand($"{SelectColumns} WHERE Id = @Id;", connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        return await ReadSingleAsync(command, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand($"{SelectColumns} WHERE Email = @Email;", connection);
        AddEmail(command, email);

        return await ReadSingleAsync(command, cancellationToken);
    }

    public async Task<int> AddAsync(User user, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.Users (Email, PasswordHash, CreatedAtUtc)
            OUTPUT INSERTED.Id
            VALUES (@Email, @PasswordHash, @CreatedAtUtc);
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        AddEmail(command, user.Email);
        command.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, PasswordHashMaxLength).Value = user.PasswordHash;
        command.Parameters.Add("@CreatedAtUtc", SqlDbType.DateTime2).Value = user.CreatedAtUtc;

        try
        {
            return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
        }
        catch (SqlException ex) when (ex.Number is UniqueConstraintViolation or UniqueIndexViolation)
        {
            // Two registrations raced past the service's existence check; UQ_Users_Email is the final word.
            throw new BusinessRuleViolationException("Email is already registered.");
        }
    }

    private static async Task<User?> ReadSingleAsync(SqlCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    private static User Map(SqlDataReader reader) =>
        User.Rehydrate(
            id: reader.GetInt32(0),
            email: reader.GetString(1),
            passwordHash: reader.GetString(2),
            createdAtUtc: reader.GetDateTime(3));

    private static void AddEmail(SqlCommand command, string email) =>
        command.Parameters.Add("@Email", SqlDbType.NVarChar, User.EmailMaxLength).Value = email;
}
