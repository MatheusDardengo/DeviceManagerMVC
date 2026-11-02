using DeviceManagerMVC.Domain.Entities;

namespace DeviceManagerMVC.Application.Abstractions;

public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);

public interface IAccessTokenIssuer
{
    AccessToken Issue(User user);
}
