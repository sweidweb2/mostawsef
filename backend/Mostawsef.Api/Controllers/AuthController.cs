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
public class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthPolicies.LoginRateLimit)]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(request, cancellationToken);
        return result is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid username or password.")
            : Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await auth.SignOutAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        if (CurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var user = await auth.GetUserAsync(userId, cancellationToken);
        return user is null ? Unauthorized() : Ok(user);
    }

    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await auth.ChangePasswordAsync(userId, request, cancellationToken));
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId) ? userId : null;
}
