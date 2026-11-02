using System.Security.Cryptography;
using System.Text;
using DeviceManagerMVC.Domain.Entities;
using DeviceManagerMVC.Infrastructure.Security;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace DeviceManagerMVC.IntegrationTests.Infrastructure.Security;

public sealed class RefreshTokenGeneratorTests
{
    private readonly RefreshTokenGenerator _sut = new();

    [Fact]
    public void Generate_returns_64_random_bytes_encoded_as_base64url()
    {
        var token = _sut.Generate();

        // Base64Url: safe in a cookie without escaping (no '+', '/' or '=' padding).
        token.RawToken.ShouldNotContain('+');
        token.RawToken.ShouldNotContain('/');
        token.RawToken.ShouldNotContain('=');
        Base64UrlEncoder.DecodeBytes(token.RawToken).Length.ShouldBe(RefreshTokenGenerator.TokenSizeInBytes);
    }

    [Fact]
    public void Generate_returns_the_sha256_of_the_raw_token_as_hash()
    {
        var token = _sut.Generate();

        token.Hash.Length.ShouldBe(RefreshToken.TokenHashLength);
        token.Hash.ShouldBe(SHA256.HashData(Encoding.UTF8.GetBytes(token.RawToken)));
    }

    [Fact]
    public void Generate_returns_a_different_token_every_time()
    {
        _sut.Generate().RawToken.ShouldNotBe(_sut.Generate().RawToken);
    }

    [Fact]
    public void ComputeHash_matches_the_hash_returned_by_Generate()
    {
        var token = _sut.Generate();

        _sut.ComputeHash(token.RawToken).ShouldBe(token.Hash);
    }
}
