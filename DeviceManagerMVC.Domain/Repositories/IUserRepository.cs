using DeviceManagerMVC.Domain.Entities;

namespace DeviceManagerMVC.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>Expects the normalized email (see <see cref="User.NormalizeEmail"/>).</summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>Inserts the user and returns the database-generated id.</summary>
    Task<int> AddAsync(User user, CancellationToken cancellationToken);
}
