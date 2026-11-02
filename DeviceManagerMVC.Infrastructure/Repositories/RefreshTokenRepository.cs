using DeviceManagerMVC.Domain.Entities;
using DeviceManagerMVC.Domain.Repositories;
using DeviceManagerMVC.Infrastructure.Persistence;

namespace DeviceManagerMVC.Infrastructure.Repositories;

public sealed class RefreshTokenRepository(SqlConnectionFactory connectionFactory) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(byte[] tokenHash, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task AddAsync(RefreshToken token, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<bool> RotateAsync(RefreshToken current, RefreshToken replacement, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task RevokeFamilyAsync(Guid familyId, DateTime revokedAtUtc, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
