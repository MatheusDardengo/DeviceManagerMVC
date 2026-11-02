using DeviceManagerMVC.Domain.Entities;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace DeviceManagerMVC.UnitTests.Domain.Entities;

public sealed class RefreshTokenTests
{
    private const int UserId = 3;

    private static readonly DateTimeOffset Now = new(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private readonly FakeTimeProvider _time = new(Now);

    private static byte[] HashOf(byte seed) => Enumerable.Repeat(seed, RefreshToken.TokenHashLength).ToArray();

    private RefreshToken StartFamily(byte seed = 1) =>
        RefreshToken.StartFamily(UserId, HashOf(seed), Lifetime, _time);

    // ---------- StartFamily ----------

    [Fact]
    public void StartFamily_creates_active_token_with_expiry_from_now()
    {
        var token = StartFamily();

        token.UserId.ShouldBe(UserId);
        token.TokenHash.ShouldBe(HashOf(1));
        token.FamilyId.ShouldNotBe(Guid.Empty);
        token.CreatedAtUtc.ShouldBe(Now.UtcDateTime);
        token.ExpiresAtUtc.ShouldBe(Now.Add(Lifetime).UtcDateTime);
        token.RevokedAtUtc.ShouldBeNull();
        token.ReplacedByTokenHash.ShouldBeNull();
        token.IsRevoked.ShouldBeFalse();
        token.IsActive(_time).ShouldBeTrue();
    }

    [Fact]
    public void StartFamily_starts_a_new_family_every_time()
    {
        StartFamily(1).FamilyId.ShouldNotBe(StartFamily(2).FamilyId);
    }

    [Fact]
    public void StartFamily_copies_the_hash_so_callers_cannot_mutate_it()
    {
        var hash = HashOf(1);
        var token = RefreshToken.StartFamily(UserId, hash, Lifetime, _time);

        hash[0] = 99;

        token.TokenHash[0].ShouldBe((byte)1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(33)]
    public void StartFamily_rejects_hash_that_is_not_sha256_sized(int length)
    {
        Should.Throw<ArgumentException>(
            () => RefreshToken.StartFamily(UserId, new byte[length], Lifetime, _time));
    }

    [Fact]
    public void StartFamily_rejects_null_hash()
    {
        Should.Throw<ArgumentNullException>(
            () => RefreshToken.StartFamily(UserId, null!, Lifetime, _time));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void StartFamily_rejects_non_positive_user_id(int userId)
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => RefreshToken.StartFamily(userId, HashOf(1), Lifetime, _time));
    }

    [Fact]
    public void StartFamily_rejects_non_positive_lifetime()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => RefreshToken.StartFamily(UserId, HashOf(1), TimeSpan.Zero, _time));
    }

    [Fact]
    public void StartFamily_throws_ArgumentNullException_when_time_provider_is_null()
    {
        Should.Throw<ArgumentNullException>(
            () => RefreshToken.StartFamily(UserId, HashOf(1), Lifetime, null!));
    }

    // ---------- Expiry ----------

    [Fact]
    public void Token_is_active_just_before_expiry()
    {
        var token = StartFamily();
        _time.Advance(Lifetime - TimeSpan.FromTicks(1));

        token.IsExpired(_time).ShouldBeFalse();
        token.IsActive(_time).ShouldBeTrue();
    }

    [Fact]
    public void Token_is_expired_at_expiry_time()
    {
        var token = StartFamily();
        _time.Advance(Lifetime);

        token.IsExpired(_time).ShouldBeTrue();
        token.IsActive(_time).ShouldBeFalse();
    }

    // ---------- Revoke ----------

    [Fact]
    public void Revoke_marks_token_revoked_and_inactive()
    {
        var token = StartFamily();

        token.Revoke(_time);

        token.IsRevoked.ShouldBeTrue();
        token.RevokedAtUtc.ShouldBe(Now.UtcDateTime);
        token.IsActive(_time).ShouldBeFalse();
    }

    [Fact]
    public void Revoke_twice_keeps_the_first_revocation_time()
    {
        var token = StartFamily();
        token.Revoke(_time);
        _time.Advance(TimeSpan.FromMinutes(5));

        token.Revoke(_time);

        token.RevokedAtUtc.ShouldBe(Now.UtcDateTime);
    }

    // ---------- Rotate ----------

    [Fact]
    public void Rotate_revokes_current_links_replacement_and_returns_new_token_in_same_family()
    {
        var current = StartFamily(1);
        _time.Advance(TimeSpan.FromMinutes(10));

        var next = current.Rotate(HashOf(2), Lifetime, _time);

        current.IsRevoked.ShouldBeTrue();
        current.RevokedAtUtc.ShouldBe(Now.AddMinutes(10).UtcDateTime);
        current.ReplacedByTokenHash.ShouldBe(HashOf(2));

        next.UserId.ShouldBe(UserId);
        next.FamilyId.ShouldBe(current.FamilyId);
        next.TokenHash.ShouldBe(HashOf(2));
        next.CreatedAtUtc.ShouldBe(Now.AddMinutes(10).UtcDateTime);
        next.ExpiresAtUtc.ShouldBe(Now.AddMinutes(10).Add(Lifetime).UtcDateTime);
        next.IsActive(_time).ShouldBeTrue();
    }

    [Fact]
    public void Rotate_revoked_token_throws()
    {
        var token = StartFamily();
        token.Revoke(_time);

        Should.Throw<InvalidOperationException>(() => token.Rotate(HashOf(2), Lifetime, _time));
    }

    [Fact]
    public void Rotate_expired_token_throws()
    {
        var token = StartFamily();
        _time.Advance(Lifetime);

        Should.Throw<InvalidOperationException>(() => token.Rotate(HashOf(2), Lifetime, _time));
    }

    [Fact]
    public void Rotate_rejects_hash_that_is_not_sha256_sized()
    {
        var token = StartFamily();

        Should.Throw<ArgumentException>(() => token.Rotate(new byte[16], Lifetime, _time));
        token.IsRevoked.ShouldBeFalse();
    }

    // ---------- Rehydrate ----------

    [Fact]
    public void Rehydrate_restores_values_and_marks_timestamps_as_utc()
    {
        var familyId = Guid.NewGuid();
        var created = new DateTime(2025, 5, 1, 8, 0, 0, DateTimeKind.Unspecified);
        var expires = created.AddDays(7);
        var revoked = created.AddDays(1);

        var token = RefreshToken.Rehydrate(UserId, HashOf(1), familyId, created, expires, revoked, HashOf(2));

        token.UserId.ShouldBe(UserId);
        token.TokenHash.ShouldBe(HashOf(1));
        token.FamilyId.ShouldBe(familyId);
        token.CreatedAtUtc.ShouldBe(created);
        token.ExpiresAtUtc.ShouldBe(expires);
        token.RevokedAtUtc.ShouldBe(revoked);
        token.ReplacedByTokenHash.ShouldBe(HashOf(2));
        token.CreatedAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
        token.ExpiresAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
        token.RevokedAtUtc!.Value.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void Rehydrate_keeps_null_revocation_for_active_token()
    {
        var at = new DateTime(2025, 5, 1, 8, 0, 0, DateTimeKind.Utc);

        var token = RefreshToken.Rehydrate(UserId, HashOf(1), Guid.NewGuid(), at, at.AddDays(7), null, null);

        token.RevokedAtUtc.ShouldBeNull();
        token.ReplacedByTokenHash.ShouldBeNull();
        token.IsRevoked.ShouldBeFalse();
    }
}
