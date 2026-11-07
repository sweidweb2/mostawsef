using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Mostawsef.Application.Interfaces;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Infrastructure.Services.Auth;

public class JwtTokenService(IOptions<JwtOptions> jwtOptions, TimeProvider time) : ITokenService
{
    private readonly JsonWebTokenHandler _handler = new();
    private readonly JwtOptions _options = jwtOptions.Value;
    private readonly SymmetricSecurityKey _signingKey = jwtOptions.Value.CreateSigningKey();

    public AccessToken CreateAccessToken(User user)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var expiresAt = NextLocalMidnightUtc();

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim(JwtClaims.Role, user.Role.ToString()),
            ]),
        });

        return new AccessToken(token, expiresAt);
    }

    /// <summary>Sessions end at the server's midnight, so staff sign in once per working day.</summary>
    private DateTime NextLocalMidnightUtc()
    {
        var midnight = time.GetLocalNow().Date.AddDays(1);
        // GetUtcOffset (not ConvertTimeToUtc): midnight does not exist on days a DST change starts at 00:00.
        return new DateTimeOffset(midnight, time.LocalTimeZone.GetUtcOffset(midnight)).UtcDateTime;
    }
}
