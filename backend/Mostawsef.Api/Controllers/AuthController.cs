using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using Mostawsef.Api.Extensions;
using Mostawsef.Application.Modules.Auth;
using Mostawsef.Contracts.Auth;

namespace Mostawsef.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService auth, IHostEnvironment environment) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthPolicies.LoginRateLimit)]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(request, cancellationToken);
        return result is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid username or password.")
            : Ok(result);
    }

    [HttpGet("clinic-access")]
    [AllowAnonymous]
    [ProducesResponseType<ClinicAccessResponse>(StatusCodes.Status200OK)]
    public async Task<ClinicAccessResponse> ClinicAccess(CancellationToken cancellationToken) =>
        await auth.GetClinicAccessAsync(cancellationToken);

    [HttpPost("clinic-session")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthPolicies.LoginRateLimit)]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ClinicSession(CancellationToken cancellationToken)
    {
        if (!IsLocalRequest())
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return Ok(await auth.OpenClinicSessionAsync(cancellationToken));
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await auth.SignOutAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await auth.ChangePasswordAsync(userId, request, cancellationToken));
    }

    /// <summary>
    /// Clinic entry is for the one-PC install (API on localhost). TestServer leaves RemoteIpAddress null — allow that
    /// in the Testing environment so integration tests can open a clinic session.
    /// </summary>
    private bool IsLocalRequest()
    {
        var ip = HttpContext.Connection.RemoteIpAddress;
        if (ip is null)
        {
            return environment.IsEnvironment("Testing");
        }

        return IPAddress.IsLoopback(ip);
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId) ? userId : null;
}
