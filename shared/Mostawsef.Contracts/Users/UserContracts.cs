using System.ComponentModel.DataAnnotations;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;

namespace Mostawsef.Contracts.Users;

/// <summary>Limits shared by the API and the desktop forms.</summary>
public static class UserRules
{
    public const int UsernameMaxLength = 50;
    public const int FullNameMaxLength = 100;
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;

    /// <summary>Letters, digits and . _ @ - (e.g. an email address). Surrounding spaces are trimmed by the API.</summary>
    public const string UsernamePattern = @"^\s*[A-Za-z0-9._@-]+\s*$";
}

public record UserListItemDto(
    Guid Id,
    string Username,
    string FullName,
    string Role,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    int Version);

public record UserDetailsDto(
    Guid Id,
    string Username,
    string FullName,
    string Role,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    int Version);

public record CreateUserRequest
{
    [Required(ErrorMessage = ErrorCodes.Required)]
    [MaxLength(UserRules.FullNameMaxLength, ErrorMessage = ErrorCodes.MaxLength)]
    public string FullName { get; init; } = string.Empty;

    [Required(ErrorMessage = ErrorCodes.Required)]
    [MaxLength(UserRules.UsernameMaxLength, ErrorMessage = ErrorCodes.MaxLength)]
    [RegularExpression(UserRules.UsernamePattern, ErrorMessage = ErrorCodes.Invalid)]
    public string Username { get; init; } = string.Empty;

    [Required(ErrorMessage = ErrorCodes.Required)]
    [MinLength(UserRules.PasswordMinLength, ErrorMessage = ErrorCodes.MinLength)]
    [MaxLength(UserRules.PasswordMaxLength, ErrorMessage = ErrorCodes.MaxLength)]
    public string Password { get; init; } = string.Empty;

    [Required(ErrorMessage = ErrorCodes.Required)]
    [AllowedValues(Roles.Admin, Roles.Super, ErrorMessage = ErrorCodes.Invalid)]
    public string Role { get; init; } = Roles.Admin;
}

/// <summary>Version is the value from the list/details DTO; a stale one returns 409 concurrency_conflict.</summary>
public record UpdateUserRequest
{
    [Required(ErrorMessage = ErrorCodes.Required)]
    [MaxLength(UserRules.FullNameMaxLength, ErrorMessage = ErrorCodes.MaxLength)]
    public string FullName { get; init; } = string.Empty;

    [Required(ErrorMessage = ErrorCodes.Required)]
    [AllowedValues(Roles.Admin, Roles.Super, ErrorMessage = ErrorCodes.Invalid)]
    public string Role { get; init; } = Roles.Admin;

    public bool IsActive { get; init; } = true;

    public int Version { get; init; }
}

public record ResetPasswordRequest
{
    [Required(ErrorMessage = ErrorCodes.Required)]
    [MinLength(UserRules.PasswordMinLength, ErrorMessage = ErrorCodes.MinLength)]
    [MaxLength(UserRules.PasswordMaxLength, ErrorMessage = ErrorCodes.MaxLength)]
    public string NewPassword { get; init; } = string.Empty;
}
