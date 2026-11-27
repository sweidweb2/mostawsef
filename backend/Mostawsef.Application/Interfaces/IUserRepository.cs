using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Users;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Application.Interfaces;

/// <summary>Deleted users are invisible to every method except <see cref="UsernameExistsAsync"/>.</summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Details projection without PasswordHash (no tracking). Null when missing.</summary>
    Task<UserDetailsDto?> GetDetailsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Active flag and role only (no tracking). Null when the user is missing or soft-deleted.</summary>
    Task<UserAuthStatus?> GetAuthStatusAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lookup by normalized (trimmed, lower-case) username.</summary>
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken);

    /// <summary>True if any user, including a deleted one, has this normalized username.</summary>
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken);

    /// <summary>Ordered by full name. Search matches the username or the normalized full name. No password hash.</summary>
    Task<(IReadOnlyList<UserListItemDto> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken);

    void Add(User user);

    /// <summary>Soft delete, applied on save.</summary>
    void Remove(User user);

    /// <summary>Stores the sign-in time directly, without counting as an edit of the user (no Version change).</summary>
    Task RecordLoginAsync(Guid userId, DateTime at, CancellationToken cancellationToken);

    /// <summary>Bumps TokenVersion (not Version) so every token issued so far stops working.</summary>
    Task RevokeTokensAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Throws AppException (409 concurrency_conflict) if the user changed since it was loaded.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
