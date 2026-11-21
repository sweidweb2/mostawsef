using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Users;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Application.Modules.Users;

/// <summary>User management (Super portal). Failures throw <see cref="AppException"/>.</summary>
public interface IUserService
{
    Task<PagedResult<UserListItemDto>> GetPageAsync(PageQuery query, CancellationToken cancellationToken);

    Task<UserDetailsDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<UserDetailsDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);

    Task<UserDetailsDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken);

    Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public class UserService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ICurrentUser currentUser,
    IUserAuthCache authCache,
    IActivityLogger activity) : IUserService
{
    public async Task<PagedResult<UserListItemDto>> GetPageAsync(PageQuery query, CancellationToken cancellationToken)
    {
        var (items, total) = await users.GetPageAsync(query, cancellationToken);
        return new PagedResult<UserListItemDto>(
            items.Select(u => new UserListItemDto(
                u.Id, u.Username, u.FullName, u.Role.ToString(), u.IsActive, u.LastLoginAt, u.CreatedAt, u.Version)).ToList(),
            query.SafePage,
            query.SafePageSize,
            total);
    }

    public async Task<UserDetailsDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await users.GetDetailsAsync(id, cancellationToken) ?? throw AppException.NotFound();
        if (user.Role != UserRole.Super)
        {
            throw AppException.NotFound();
        }

        return new UserDetailsDto(
            user.Id, user.Username, user.FullName, user.Role.ToString(), user.IsActive, user.LastLoginAt,
            user.CreatedAt, user.CreatedBy, user.UpdatedAt, user.UpdatedBy, user.Version);
    }

    public async Task<UserDetailsDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var username = User.NormalizeUsername(request.Username);
        if (username == ClinicIdentity.Username)
        {
            throw AppException.Conflict(ErrorCodes.UsernameTaken, nameof(CreateUserRequest.Username));
        }

        if (await users.UsernameExistsAsync(username, cancellationToken))
        {
            throw AppException.Conflict(ErrorCodes.UsernameTaken, nameof(CreateUserRequest.Username));
        }

        var role = ParseRole(request.Role);
        if (role != UserRole.Super)
        {
            throw AppException.Validation(nameof(CreateUserRequest.Role), ErrorCodes.Invalid);
        }

        var user = new User(
            username, request.FullName, SearchText.Normalize(request.FullName), passwordHasher.Hash(request.Password), role);
        users.Add(user);
        try
        {
            await users.SaveChangesAsync(cancellationToken);
        }
        catch (AppException ex) when (ex.Kind == AppErrorKind.Conflict && ex.Code == ErrorCodes.Taken)
        {
            // Lost a race with another request creating the same username after the check above.
            throw AppException.Conflict(ErrorCodes.UsernameTaken, nameof(CreateUserRequest.Username));
        }

        await LogAsync(ActivityActions.UserCreated, "Created a user", user, cancellationToken);
        return ToDetails(user);
    }

    public async Task<UserDetailsDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await FindManagedAsync(id, cancellationToken);
        if (user.Version != request.Version)
        {
            throw AppException.Conflict(ErrorCodes.ConcurrencyConflict);
        }

        var role = ParseRole(request.Role);
        if (role != UserRole.Super)
        {
            throw AppException.Validation(nameof(UpdateUserRequest.Role), ErrorCodes.Invalid);
        }

        if (IsCurrentUser(user) && (!request.IsActive || role != user.Role))
        {
            throw AppException.Conflict(ErrorCodes.OwnAccount);
        }

        user.Rename(request.FullName, SearchText.Normalize(request.FullName));
        user.ChangeRole(role);
        if (request.IsActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }

        await users.SaveChangesAsync(cancellationToken);
        authCache.Invalidate(user.Id);

        await LogAsync(ActivityActions.UserUpdated, "Updated a user", user, cancellationToken);
        return ToDetails(user);
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await FindManagedAsync(id, cancellationToken);
        if (IsCurrentUser(user))
        {
            throw AppException.Conflict(ErrorCodes.OwnAccount);
        }

        user.ChangePasswordHash(passwordHasher.Hash(request.NewPassword));
        await users.SaveChangesAsync(cancellationToken);
        authCache.Invalidate(user.Id);

        await LogAsync(ActivityActions.UserPasswordReset, "Reset a user's password", user, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await FindManagedAsync(id, cancellationToken);
        if (IsCurrentUser(user))
        {
            throw AppException.Conflict(ErrorCodes.OwnAccount);
        }

        users.Remove(user);
        await users.SaveChangesAsync(cancellationToken);
        authCache.Invalidate(user.Id);

        await LogAsync(ActivityActions.UserDeleted, "Deleted a user", user, cancellationToken);
    }

    /// <summary>Managed Super accounts only; the clinic Admin identity is not editable.</summary>
    private async Task<User> FindManagedAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(id, cancellationToken) ?? throw AppException.NotFound();
        if (user.Role != UserRole.Super || user.Username == ClinicIdentity.Username)
        {
            throw AppException.NotFound();
        }

        return user;
    }

    private bool IsCurrentUser(User user) => currentUser.UserId == user.Id;

    private static UserRole ParseRole(string role) => Enum.Parse<UserRole>(role);

    private Task LogAsync(string action, string description, User user, CancellationToken cancellationToken) =>
        activity.LogAsync(
            new ActivityEntry(action, ActivityModules.Users, description)
            {
                EntityType = nameof(User),
                EntityId = user.Id.ToString(),
                Details = new { user.Username, Role = user.Role.ToString(), user.IsActive },
            },
            cancellationToken);

    private static UserDetailsDto ToDetails(User u) => new(
        u.Id, u.Username, u.FullName, u.Role.ToString(), u.IsActive, u.LastLoginAt,
        u.CreatedAt, u.CreatedBy, u.UpdatedAt, u.UpdatedBy, u.Version);
}
