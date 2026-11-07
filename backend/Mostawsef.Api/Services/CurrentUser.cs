using System.Diagnostics;
using Microsoft.IdentityModel.JsonWebTokens;
using Mostawsef.Application.Interfaces;
using Mostawsef.Infrastructure.Services.Auth;

namespace Mostawsef.Api.Services;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private HttpContext? Context => accessor.HttpContext;

    public Guid? UserId =>
        Guid.TryParse(Context?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;

    public string? Username => Context?.User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value;

    public string? Role => Context?.User.FindFirst(JwtClaims.Role)?.Value;

    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => Context?.Request.Headers.UserAgent.ToString() is { Length: > 0 } agent ? agent : null;

    public string? RequestPath => Context?.Request.Path.Value;

    public string? RequestMethod => Context?.Request.Method;

    public string? TraceId => Activity.Current?.Id ?? Context?.TraceIdentifier;
}
