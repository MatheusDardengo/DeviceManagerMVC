using DeviceManagerMVC.Infrastructure.Security;
using Shouldly;

namespace DeviceManagerMVC.IntegrationTests.Infrastructure.Security;

public sealed class IdentityPasswordHasherTests
{
    private const string Password = "Passw0rd!";

    private readonly IdentityPasswordHasher _sut = new();

    [Fact]
    public void Hash_never_returns_the_plain_password()
    {
        _sut.Hash(Password).ShouldNotContain(Password);
    }

    [Fact]
    public void Hash_is_salted_so_the_same_password_hashes_differently()
    {
        _sut.Hash(Password).ShouldNotBe(_sut.Hash(Password));
    }

    [Fact]
    public void Hash_fits_the_PasswordHash_column()
    {
        // NVARCHAR(512) in 02_create_tables.sql.
        _sut.Hash(Password).Length.ShouldBeLessThanOrEqualTo(512);
    }

    [Fact]
    public void Verify_accepts_the_correct_password()
    {
        _sut.Verify(_sut.Hash(Password), Password).ShouldBeTrue();
    }

    [Fact]
    public void Verify_rejects_a_wrong_password()
    {
        _sut.Verify(_sut.Hash(Password), "wrong-password").ShouldBeFalse();
    }
}
