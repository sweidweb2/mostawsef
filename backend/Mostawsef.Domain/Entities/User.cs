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

    public User(string username, string fullName, string searchName, string passwordHash, UserRole role)
    {
        Username = NormalizeUsername(username);
        PasswordHash = passwordHash;
        Role = role;
        Rename(fullName, searchName);
    }

    /// <summary>Lower-case, trimmed. Unique, also among deleted users.</summary>
    public string Username { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    /// <summary>FullName normalized with Contracts SearchText (by the caller).</summary>
    public string SearchName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>Bumped on every password change so existing JWTs stop working on the next request.</summary>
    public int TokenVersion { get; private set; }

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTime? LastLoginAt { get; private set; }

    public static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();

    public void Rename(string fullName, string searchName)
    {
        FullName = fullName.Trim();
        SearchName = searchName;
    }

    public void ChangeRole(UserRole role) => Role = role;

    public void ChangePasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        TokenVersion++;
    }

    public void RecordLogin(DateTime at) => LastLoginAt = at;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
