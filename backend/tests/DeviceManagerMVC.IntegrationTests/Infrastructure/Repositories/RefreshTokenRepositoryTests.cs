using DeviceManagerMVC.Domain.Entities;
using DeviceManagerMVC.Infrastructure.Persistence;
using DeviceManagerMVC.Infrastructure.Repositories;
using DeviceManagerMVC.IntegrationTests.Infrastructure.Database;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace DeviceManagerMVC.IntegrationTests.Infrastructure.Repositories;

[Collection(DatabaseCollection.Name)]
public sealed class RefreshTokenRepositoryTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private readonly FakeTimeProvider _time = new(Now);
    private readonly RefreshTokenRepository _sut = new(new SqlConnectionFactory(fixture.ConnectionString));
    private readonly CancellationToken _ct = CancellationToken.None;
    private int _userId;

    public async Task InitializeAsync()
    {
        await fixture.ResetUsersAsync();

        _userId = await new UserRepository(new SqlConnectionFactory(fixture.ConnectionString))
            .AddAsync(User.Create("user@example.com", "hash", _time), _ct);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static byte[] HashOf(byte seed) => Enumerable.Repeat(seed, RefreshToken.TokenHashLength).ToArray();

    private async Task<RefreshToken> GivenStoredToken(byte seed = 1)
    {
        var token = RefreshToken.StartFamily(_userId, HashOf(seed), Lifetime, _time);
        await _sut.AddAsync(token, _ct);
        return token;
    }

    // ---------- Add / GetByHash ----------

    [Fact]
    public async Task Add_persists_every_field()
    {
        var token = await GivenStoredToken();

        var stored = await _sut.GetByHashAsync(HashOf(1), _ct);

        stored.ShouldNotBeNull();
        stored.UserId.ShouldBe(_userId);
        stored.TokenHash.ShouldBe(HashOf(1));
        stored.FamilyId.ShouldBe(token.FamilyId);
        stored.CreatedAtUtc.ShouldBe(Now.UtcDateTime);
        stored.ExpiresAtUtc.ShouldBe(Now.Add(Lifetime).UtcDateTime);
        stored.RevokedAtUtc.ShouldBeNull();
        stored.ReplacedByTokenHash.ShouldBeNull();
        stored.CreatedAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
        stored.ExpiresAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task GetByHash_unknown_returns_null()
    {
        await GivenStoredToken(1);

        (await _sut.GetByHashAsync(HashOf(9), _ct)).ShouldBeNull();
    }

    // ---------- Rotate ----------

    [Fact]
    public async Task Rotate_revokes_current_links_replacement_and_inserts_it()
    {
        var current = await GivenStoredToken(1);
        _time.Advance(TimeSpan.FromMinutes(10));
        var replacement = current.Rotate(HashOf(2), Lifetime, _time);

        var rotated = await _sut.RotateAsync(current, replacement, _ct);

        rotated.ShouldBeTrue();
        var storedCurrent = await _sut.GetByHashAsync(HashOf(1), _ct);
        storedCurrent!.RevokedAtUtc.ShouldBe(Now.AddMinutes(10).UtcDateTime);
        storedCurrent.RevokedAtUtc!.Value.Kind.ShouldBe(DateTimeKind.Utc);
        storedCurrent.ReplacedByTokenHash.ShouldBe(HashOf(2));

        var storedReplacement = await _sut.GetByHashAsync(HashOf(2), _ct);
        storedReplacement.ShouldNotBeNull();
        storedReplacement.FamilyId.ShouldBe(current.FamilyId);
        storedReplacement.IsRevoked.ShouldBeFalse();
    }

    [Fact]
    public async Task Rotate_returns_false_and_inserts_nothing_when_current_was_already_revoked()
    {
        // Simulates two requests rotating the same token: the first one wins.
        await GivenStoredToken(1);
        var first = await _sut.GetByHashAsync(HashOf(1), _ct);
        var second = await _sut.GetByHashAsync(HashOf(1), _ct);
        await _sut.RotateAsync(first!, first!.Rotate(HashOf(2), Lifetime, _time), _ct);

        var rotated = await _sut.RotateAsync(second!, second!.Rotate(HashOf(3), Lifetime, _time), _ct);

        rotated.ShouldBeFalse();
        (await _sut.GetByHashAsync(HashOf(3), _ct)).ShouldBeNull();
        (await _sut.GetByHashAsync(HashOf(1), _ct))!.ReplacedByTokenHash.ShouldBe(HashOf(2));
    }

    // ---------- RevokeFamily ----------

    [Fact]
    public async Task RevokeFamily_revokes_active_tokens_of_that_family_only()
    {
        var current = await GivenStoredToken(1);
        await _sut.RotateAsync(current, current.Rotate(HashOf(2), Lifetime, _time), _ct);
        var otherFamily = await GivenStoredToken(3);
        _time.Advance(TimeSpan.FromMinutes(10));

        await _sut.RevokeFamilyAsync(current.FamilyId, _time.GetUtcNow().UtcDateTime, _ct);

        (await _sut.GetByHashAsync(HashOf(2), _ct))!.RevokedAtUtc.ShouldBe(Now.AddMinutes(10).UtcDateTime);
        (await _sut.GetByHashAsync(otherFamily.TokenHash, _ct))!.IsRevoked.ShouldBeFalse();
    }

    [Fact]
    public async Task RevokeFamily_keeps_the_original_revocation_time_of_already_revoked_tokens()
    {
        var current = await GivenStoredToken(1);
        await _sut.RotateAsync(current, current.Rotate(HashOf(2), Lifetime, _time), _ct);
        _time.Advance(TimeSpan.FromMinutes(10));

        await _sut.RevokeFamilyAsync(current.FamilyId, _time.GetUtcNow().UtcDateTime, _ct);

        (await _sut.GetByHashAsync(HashOf(1), _ct))!.RevokedAtUtc.ShouldBe(Now.UtcDateTime);
    }
}
