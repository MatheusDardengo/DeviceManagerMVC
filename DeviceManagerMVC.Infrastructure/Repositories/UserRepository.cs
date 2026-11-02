using DeviceManagerMVC.Domain.Entities;
using DeviceManagerMVC.Domain.Repositories;
using DeviceManagerMVC.Infrastructure.Persistence;

namespace DeviceManagerMVC.Infrastructure.Repositories;

public sealed class UserRepository(SqlConnectionFactory connectionFactory) : IUserRepository
{
    public Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<int> AddAsync(User user, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
