using Mostawsef.Domain.Common;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Domain.Entities;

/// <summary>
/// Application user. Only a password hash is ever stored, never the password.
/// Deleted users are kept (soft delete) and their usernames are never reused, so logs stay unambiguous.
/// </summary>
public class User : SoftDeletableEntity
{
    public const int UsernameMaxLength = 50;
    public const int FullNameMaxLength = 100;

    private User()
    {
    }

    public User(string username, string fullName, string passwordHash, UserRole role)
    {
        Username = NormalizeUsername(username);
        PasswordHash = passwordHash;
        Role = role;
        Rename(fullName);
    }

    /// <summary>Lower-case, trimmed. Unique, also among deleted users.</summary>
    public string Username { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    /// <summary>FullName normalized with <see cref="SearchText"/>.</summary>
    public string SearchName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTime? LastLoginAt { get; private set; }

    public static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();

    public void Rename(string fullName)
    {
        FullName = fullName.Trim();
        SearchName = SearchText.Normalize(FullName);
    }

    public void ChangeRole(UserRole role) => Role = role;

    public void ChangePasswordHash(string passwordHash) => PasswordHash = passwordHash;

    public void RecordLogin(DateTime at) => LastLoginAt = at;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
