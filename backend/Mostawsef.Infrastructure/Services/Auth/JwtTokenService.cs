using System.Globalization;
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

    /// <summary>Late-day sign-ins keep at least this much session time (rolls to the midnight after next).</summary>
    private static readonly TimeSpan MinSessionRemaining = TimeSpan.FromHours(1);

    public AccessToken CreateAccessToken(User user)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var expiresAt = SessionExpiresAtUtc(now);

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
                new Claim(JwtClaims.TokenVersion, user.TokenVersion.ToString(CultureInfo.InvariantCulture)),
            ]),
        });

        return new AccessToken(token, expiresAt, expiresAt - now);
    }

    /// <summary>
    /// Sessions end at the server's local midnight so staff sign in once per working day.
    /// If that is under an hour away, expire at the following midnight instead.
    /// </summary>
    private DateTime SessionExpiresAtUtc(DateTime nowUtc)
    {
        var expiresAt = LocalMidnightUtc(daysAhead: 1);
        if (expiresAt - nowUtc < MinSessionRemaining)
        {
            expiresAt = LocalMidnightUtc(daysAhead: 2);
        }

        return expiresAt;
    }

    private DateTime LocalMidnightUtc(int daysAhead)
    {
        var midnight = time.GetLocalNow().Date.AddDays(daysAhead);
        // GetUtcOffset (not ConvertTimeToUtc): midnight does not exist on days a DST change starts at 00:00.
        return new DateTimeOffset(midnight, time.LocalTimeZone.GetUtcOffset(midnight)).UtcDateTime;
    }
}
