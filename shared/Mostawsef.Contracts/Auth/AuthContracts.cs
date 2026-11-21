using System.ComponentModel.DataAnnotations;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Users;

namespace Mostawsef.Contracts.Auth;

/// <summary>Both fields are required and may not be empty or whitespace (the API answers 400 otherwise).</summary>
public record LoginRequest([Required] string Username, [Required] string Password);

/// <summary>
/// ExpiresInSeconds is the session length as the server sees it. The desktop counts it from its own receipt of the
/// reply, so a PC whose clock is wrong does not read the session as already expired. 0 (older servers) means: use ExpiresAt.
/// </summary>
public record LoginResponse(string AccessToken, DateTime ExpiresAt, UserDto User, int ExpiresInSeconds = 0);

public record UserDto(Guid Id, string Username, string Role, string? FullName = null);

/// <summary>The signed-in user changes their own password. A wrong CurrentPassword returns 400 (wrong_password).</summary>
public record ChangePasswordRequest
{
    [Required(ErrorMessage = ErrorCodes.Required)]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required(ErrorMessage = ErrorCodes.Required)]
    [MinLength(UserRules.PasswordMinLength, ErrorMessage = ErrorCodes.MinLength)]
    [MaxLength(UserRules.PasswordMaxLength, ErrorMessage = ErrorCodes.MaxLength)]
    public string NewPassword { get; init; } = string.Empty;
}

/// <summary>Role names shared by the API (authorization) and the desktop app (portal routing).</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Super = "Super";
}

/// <summary>
/// Built-in Admin identity for the passwordless clinic session. Seeded at API startup; not managed in the Users page.
/// Audit fields and activity logs use <see cref="Username"/> as the actor.
/// </summary>
public static class ClinicIdentity
{
    public const string Username = "clinic";
    public const string FullName = "Clinic";
}

/// <summary>Whether the login screen may open the Admin portal without a password (false while maintenance mode is on).</summary>
public record ClinicAccessResponse(bool Available);
