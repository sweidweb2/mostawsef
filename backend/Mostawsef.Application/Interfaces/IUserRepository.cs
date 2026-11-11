using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Application.Interfaces;

/// <summary>One row of the users list: no password hash.</summary>
public record UserListRow(
    Guid Id,
    string Username,
    string FullName,
    UserRole Role,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    int Version);

/// <summary>User details for a read-only GET (no password hash, no tracking).</summary>
public record UserDetailsRow(
    Guid Id,
    string Username,
    string FullName,
    UserRole Role,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    int Version);

/// <summary>Deleted users are invisible to every method except <see cref="UsernameExistsAsync"/>.</summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Details projection without PasswordHash (no tracking). Null when missing.</summary>
    Task<UserDetailsRow?> GetDetailsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Active flag and role only (no tracking). Null when the user is missing or soft-deleted.</summary>
    Task<UserAuthStatus?> GetAuthStatusAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lookup by normalized (trimmed, lower-case) username.</summary>
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken);

    /// <summary>True if any user, including a deleted one, has this normalized username.</summary>
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken);

    /// <summary>Ordered by full name. Search matches the username or the normalized full name. No password hash.</summary>
    Task<(IReadOnlyList<UserListRow> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken);

    void Add(User user);

    /// <summary>Soft delete, applied on save.</summary>
    void Remove(User user);

    /// <summary>Stores the sign-in time directly, without counting as an edit of the user (no Version change).</summary>
    Task RecordLoginAsync(Guid userId, DateTime at, CancellationToken cancellationToken);

    /// <summary>Throws AppException (409 concurrency_conflict) if the user changed since it was loaded.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
