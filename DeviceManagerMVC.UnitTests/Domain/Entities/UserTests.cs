using DeviceManagerMVC.Domain.Common;
using DeviceManagerMVC.Domain.Entities;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace DeviceManagerMVC.UnitTests.Domain.Entities;

public sealed class UserTests
{
    private const string Hash = "AQAAAAIAAYagAAAAEhashedpasswordvalue==";

    private static readonly DateTimeOffset Now = new(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Now);

    // ---------- NormalizeEmail ----------

    [Theory]
    [InlineData("user@example.com", "user@example.com")]
    [InlineData("  User@Example.COM  ", "user@example.com")]
    public void NormalizeEmail_trims_and_lowercases(string input, string expected)
    {
        User.NormalizeEmail(input).ShouldBe(expected);
    }

    // ---------- Create ----------

    [Fact]
    public void Create_sets_normalized_email_hash_and_creation_time()
    {
        var user = User.Create("  User@Example.COM ", Hash, _time);

        user.Email.ShouldBe("user@example.com");
        user.PasswordHash.ShouldBe(Hash);
        user.CreatedAtUtc.ShouldBe(Now.UtcDateTime);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_missing_email(string? email)
    {
        Should.Throw<DomainValidationException>(() => User.Create(email!, Hash, _time));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("user@")]
    [InlineData("@example.com")]
    [InlineData("John <john@example.com>")]
    public void Create_rejects_invalid_email(string email)
    {
        Should.Throw<DomainValidationException>(() => User.Create(email, Hash, _time));
    }

    [Fact]
    public void Create_rejects_email_longer_than_max()
    {
        var email = new string('a', User.EmailMaxLength - "@example.com".Length + 1) + "@example.com";

        Should.Throw<DomainValidationException>(() => User.Create(email, Hash, _time));
    }

    [Fact]
    public void Create_accepts_email_at_max_length()
    {
        var email = new string('a', User.EmailMaxLength - "@example.com".Length) + "@example.com";

        User.Create(email, Hash, _time).Email.Length.ShouldBe(User.EmailMaxLength);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_without_password_hash_is_a_programming_error(string? hash)
    {
        // The hash comes from the hasher, not from the client: a missing one is a bug, not a 400.
        Should.Throw<ArgumentException>(() => User.Create("user@example.com", hash!, _time));
    }

    [Fact]
    public void Create_throws_ArgumentNullException_when_time_provider_is_null()
    {
        Should.Throw<ArgumentNullException>(() => User.Create("user@example.com", Hash, null!));
    }

    // ---------- Rehydrate ----------

    [Fact]
    public void Rehydrate_restores_values_and_marks_creation_time_as_utc()
    {
        var unspecified = new DateTime(2025, 5, 1, 8, 0, 0, DateTimeKind.Unspecified);

        var user = User.Rehydrate(5, "user@example.com", Hash, unspecified);

        user.Id.ShouldBe(5);
        user.Email.ShouldBe("user@example.com");
        user.PasswordHash.ShouldBe(Hash);
        user.CreatedAtUtc.ShouldBe(unspecified);
        user.CreatedAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
    }
}
