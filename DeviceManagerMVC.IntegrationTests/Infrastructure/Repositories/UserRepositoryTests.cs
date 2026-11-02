using DeviceManagerMVC.Domain.Common;
using DeviceManagerMVC.Domain.Entities;
using DeviceManagerMVC.Infrastructure.Persistence;
using DeviceManagerMVC.Infrastructure.Repositories;
using DeviceManagerMVC.IntegrationTests.Infrastructure.Database;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace DeviceManagerMVC.IntegrationTests.Infrastructure.Repositories;

[Collection(DatabaseCollection.Name)]
public sealed class UserRepositoryTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string Hash = "AQAAAAIAAYagAAAAEhashedpasswordvalue==";

    private static readonly DateTimeOffset Now = new(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Now);
    private readonly UserRepository _sut = new(new SqlConnectionFactory(fixture.ConnectionString));
    private readonly CancellationToken _ct = CancellationToken.None;

    public Task InitializeAsync() => fixture.ResetUsersAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private User NewUser(string email = "user@example.com") => User.Create(email, Hash, _time);

    [Fact]
    public async Task Add_returns_generated_id_and_persists_every_field()
    {
        var id = await _sut.AddAsync(NewUser(), _ct);

        var stored = await _sut.GetByIdAsync(id, _ct);

        id.ShouldBeGreaterThan(0);
        stored.ShouldNotBeNull();
        stored.Id.ShouldBe(id);
        stored.Email.ShouldBe("user@example.com");
        stored.PasswordHash.ShouldBe(Hash);
        stored.CreatedAtUtc.ShouldBe(Now.UtcDateTime);
        stored.CreatedAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task GetByEmail_returns_the_matching_user()
    {
        await _sut.AddAsync(NewUser("other@example.com"), _ct);
        var id = await _sut.AddAsync(NewUser("user@example.com"), _ct);

        var stored = await _sut.GetByEmailAsync("user@example.com", _ct);

        stored.ShouldNotBeNull();
        stored.Id.ShouldBe(id);
        stored.Email.ShouldBe("user@example.com");
    }

    [Fact]
    public async Task GetByEmail_unknown_returns_null()
    {
        (await _sut.GetByEmailAsync("nobody@example.com", _ct)).ShouldBeNull();
    }

    [Fact]
    public async Task GetById_unknown_returns_null()
    {
        (await _sut.GetByIdAsync(int.MaxValue, _ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Add_accepts_email_at_domain_max_length()
    {
        // Guards against drift between User.EmailMaxLength and NVARCHAR(256) in 02_create_tables.sql.
        var email = new string('a', User.EmailMaxLength - "@example.com".Length) + "@example.com";

        var id = await _sut.AddAsync(NewUser(email), _ct);

        (await _sut.GetByIdAsync(id, _ct))!.Email.Length.ShouldBe(User.EmailMaxLength);
    }

    [Fact]
    public async Task Add_duplicate_email_throws_BusinessRuleViolation()
    {
        // The service checks first; this covers two registrations racing past that check.
        await _sut.AddAsync(NewUser(), _ct);

        await Should.ThrowAsync<BusinessRuleViolationException>(() => _sut.AddAsync(NewUser(), _ct));
    }
}
