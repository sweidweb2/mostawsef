using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Interfaces;

public record AccessToken(string Token, DateTime ExpiresAt, TimeSpan Lifetime);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);
}
