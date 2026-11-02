using DeviceManagerMVC.Domain.Entities;

namespace DeviceManagerMVC.Domain.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(byte[] tokenHash, CancellationToken cancellationToken);

    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically persists the revocation of <paramref name="current"/> and inserts <paramref name="replacement"/>.
    /// Returns false when <paramref name="current"/> was already revoked by a concurrent request.
    /// </summary>
    Task<bool> RotateAsync(RefreshToken current, RefreshToken replacement, CancellationToken cancellationToken);

    /// <summary>Revokes every still-active token of the family.</summary>
    Task RevokeFamilyAsync(Guid familyId, DateTime revokedAtUtc, CancellationToken cancellationToken);
}
