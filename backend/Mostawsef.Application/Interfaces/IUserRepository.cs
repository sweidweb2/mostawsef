using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Interfaces;

/// <summary>Deleted users are invisible to every method except <see cref="UsernameExistsAsync"/>.</summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lookup by normalized (trimmed, lower-case) username.</summary>
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken);

    /// <summary>True if any user, including a deleted one, has this normalized username.</summary>
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken);

    /// <summary>Ordered by full name. Search matches the username or the normalized full name.</summary>
    Task<(IReadOnlyList<User> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken);

    void Add(User user);

    /// <summary>Soft delete, applied on save.</summary>
    void Remove(User user);

    /// <summary>Stores the sign-in time directly, without counting as an edit of the user (no Version change).</summary>
    Task RecordLoginAsync(Guid userId, DateTime at, CancellationToken cancellationToken);

    /// <summary>Throws AppException (409 concurrency_conflict) if the user changed since it was loaded.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
