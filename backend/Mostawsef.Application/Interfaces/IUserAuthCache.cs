using Mostawsef.Domain.Enums;

namespace Mostawsef.Application.Interfaces;

/// <summary>Active flag and role used by JWT re-validation. Null when the user is missing or soft-deleted.</summary>
public readonly record struct UserAuthStatus(bool IsActive, UserRole Role);

/// <summary>
/// Short-lived auth status for <c>RejectStaleUserAsync</c>. Invalidate after a user is updated or deleted so
/// deactivation and role changes take effect on the next request.
/// </summary>
public interface IUserAuthCache
{
    bool TryGet(Guid userId, out UserAuthStatus status);

    void Set(Guid userId, UserAuthStatus status);

    void Invalidate(Guid userId);
}
