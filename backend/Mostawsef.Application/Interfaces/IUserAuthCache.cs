using Mostawsef.Domain.Enums;

namespace Mostawsef.Application.Interfaces;

/// <summary>Active flag, role and token version used by JWT re-validation. Null when the user is missing or soft-deleted.</summary>
public readonly record struct UserAuthStatus(bool IsActive, UserRole Role, int TokenVersion);

/// <summary>
/// Auth status for <c>RejectStaleUserAsync</c>. Invalidate after a user is updated, deleted or their password
/// changes so deactivation, role and password changes take effect on the next request.
/// </summary>
public interface IUserAuthCache
{
    bool TryGet(Guid userId, out UserAuthStatus status);

    /// <summary>Generation to pass to <see cref="TrySet"/>; bumps when <see cref="Invalidate"/> runs.</summary>
    int Generation(Guid userId);

    /// <summary>Caches only when <paramref name="generation"/> still matches (no Invalidate since it was read).</summary>
    bool TrySet(Guid userId, int generation, UserAuthStatus status);

    void Invalidate(Guid userId);
}
