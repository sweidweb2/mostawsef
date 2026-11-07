using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Application.Modules.Auth;

public interface IAuthService
{
    /// <summary>
    /// Returns null when the credentials are invalid or the user is inactive.
    /// Throws AppException 409 maintenance_mode for non-Super users while maintenance mode is on.
    /// </summary>
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<UserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Records the sign-out of the current user (tokens are stateless, the client discards it).</summary>
    Task SignOutAsync(CancellationToken cancellationToken);

    /// <summary>Throws AppException: 400 wrong_password on CurrentPassword, 404 if the user no longer exists.</summary>
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken);
}

public class AuthService(
    IUserRepository users,
    ISystemSettingsRepository systemSettings,
    IPasswordHasher passwordHasher,
    ITokenService tokens,
    IActivityLogger activity,
    TimeProvider time) : IAuthService
{
    private static string? _dummyHash;

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var username = User.NormalizeUsername(request.Username);
        var user = await users.GetByUsernameAsync(username, cancellationToken);
        if (user is null || !user.IsActive)
        {
            // Same hashing cost as a real attempt, so response time does not reveal which usernames exist.
            passwordHasher.Verify(_dummyHash ??= passwordHasher.Hash(Guid.NewGuid().ToString()), request.Password);
            await LogFailedSignInAsync(username, cancellationToken);
            return null;
        }

        var check = passwordHasher.Verify(user.PasswordHash, request.Password);
        if (check == PasswordCheck.Failed)
        {
            await LogFailedSignInAsync(username, cancellationToken);
            return null;
        }

        if (user.Role != UserRole.Super && (await systemSettings.GetAsync(cancellationToken)).MaintenanceMode)
        {
            await activity.LogAsync(
                new ActivityEntry(ActivityActions.SignInBlocked, ActivityModules.Auth, "Sign-in blocked by maintenance mode")
                {
                    UserId = user.Id,
                    Username = user.Username,
                },
                cancellationToken);
            throw AppException.Conflict(ErrorCodes.MaintenanceMode);
        }

        if (check == PasswordCheck.SuccessRehashNeeded)
        {
            user.ChangePasswordHash(passwordHasher.Hash(request.Password));
            user.RecordLogin(time.GetUtcNow().UtcDateTime);
            await users.SaveChangesAsync(cancellationToken);
        }
        else
        {
            await users.RecordLoginAsync(user.Id, time.GetUtcNow().UtcDateTime, cancellationToken);
        }

        await activity.LogAsync(
            new ActivityEntry(ActivityActions.SignedIn, ActivityModules.Auth, "Signed in")
            {
                UserId = user.Id,
                Username = user.Username,
                Details = new { Role = user.Role.ToString() },
            },
            cancellationToken);

        var token = tokens.CreateAccessToken(user);
        return new LoginResponse(token.Token, token.ExpiresAt, ToDto(user));
    }

    public async Task<UserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        return user is { IsActive: true } ? ToDto(user) : null;
    }

    public Task SignOutAsync(CancellationToken cancellationToken) =>
        activity.LogAsync(new ActivityEntry(ActivityActions.SignedOut, ActivityModules.Auth, "Signed out"), cancellationToken);

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken) ?? throw AppException.NotFound();
        if (passwordHasher.Verify(user.PasswordHash, request.CurrentPassword) == PasswordCheck.Failed)
        {
            throw AppException.Validation(nameof(ChangePasswordRequest.CurrentPassword), ErrorCodes.WrongPassword);
        }

        user.ChangePasswordHash(passwordHasher.Hash(request.NewPassword));
        await users.SaveChangesAsync(cancellationToken);

        await activity.LogAsync(
            new ActivityEntry(ActivityActions.PasswordChanged, ActivityModules.Auth, "Changed own password")
            {
                EntityType = nameof(User),
                EntityId = user.Id.ToString(),
            },
            cancellationToken);
    }

    private Task LogFailedSignInAsync(string attemptedUsername, CancellationToken cancellationToken) =>
        activity.LogAsync(
            new ActivityEntry(ActivityActions.SignInFailed, ActivityModules.Auth, "Failed sign-in attempt")
            {
                Username = attemptedUsername,
            },
            cancellationToken);

    private static UserDto ToDto(User user) =>
        new(user.Id, user.Username, user.Role.ToString(), string.IsNullOrEmpty(user.FullName) ? null : user.FullName);
}
