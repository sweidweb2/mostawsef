using Mostawsef.Domain.Common;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Domain.Entities;

/// <summary>Application user. Only a password hash is ever stored, never the password.</summary>
public class User : Entity
{
    public const int UsernameMaxLength = 50;

    private User()
    {
    }

    public User(string username, string passwordHash, UserRole role)
    {
        Username = NormalizeUsername(username);
        PasswordHash = passwordHash;
        Role = role;
    }

    /// <summary>Lower-case, trimmed. Unique.</summary>
    public string Username { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTime? LastLoginAt { get; private set; }

    public static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();

    public void ChangePasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        Touch();
    }

    public void RecordLogin() => LastLoginAt = DateTime.UtcNow;

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }
}
