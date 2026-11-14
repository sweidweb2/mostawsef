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

    /// <summary>
    /// Throws AppException: 400 wrong_password on CurrentPassword, 404 if the user no longer exists.
    /// Returns a fresh access token (password change bumps TokenVersion and invalidates the previous JWT).
    /// </summary>
    Task<LoginResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken);
}

public class AuthService(
    IUserRepository users,
    ISystemSettingsCache systemSettings,
    IPasswordHasher passwordHasher,
    ITokenService tokens,
    IUserAuthCache authCache,
    ILoginThrottle throttle,
    IActivityLogger activity,
    TimeProvider time) : IAuthService
{
    private static string? _dummyHash;

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var username = User.NormalizeUsername(request.Username);
        if (throttle.IsBlocked(username))
        {
            // Too many failures for this name (from any IP): answer like a wrong password, without the PBKDF2 cost or a log row.
            return null;
        }

        var user = await users.GetByUsernameAsync(username, cancellationToken);
        if (user is null || !user.IsActive)
        {
            // Same hashing cost as a real attempt, so response time does not reveal which usernames exist.
            passwordHasher.Verify(DummyHash(), request.Password);
            await LogFailedSignInAsync(username, cancellationToken);
            return null;
        }

        // Overlap the settings read with PBKDF2 for non-Super users (Super skips maintenance).
        var settingsTask = user.Role == UserRole.Super ? null : systemSettings.GetAsync(cancellationToken);

        var check = await VerifyPasswordAsync(user, request.Password, settingsTask);
        if (check == PasswordCheck.Failed)
        {
            await LogFailedSignInAsync(username, cancellationToken);
            return null;
        }

        if (settingsTask is not null && (await settingsTask).MaintenanceMode)
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

        await RecordSignInAsync(user, request.Password, check, cancellationToken);

        var token = tokens.CreateAccessToken(user);
        return new LoginResponse(token.Token, token.ExpiresAt, ToDto(user));
    }

    /// <summary>
    /// Checks the password. The settings read started next to it is always awaited, so a fault in it is observed
    /// whichever way the check ends.
    /// </summary>
    private async Task<PasswordCheck> VerifyPasswordAsync(User user, string password, Task? racingSettings)
    {
        PasswordCheck check;
        try
        {
            check = passwordHasher.Verify(user.PasswordHash, password);
        }
        catch
        {
            await racingSettings.ObserveAsync();
            throw;
        }

        if (check == PasswordCheck.Failed)
        {
            await racingSettings.ObserveAsync();
        }

        return check;
    }

    /// <summary>Last login, an upgraded password hash when the hasher asks for one, and the sign-in activity.</summary>
    private async Task RecordSignInAsync(User user, string password, PasswordCheck check, CancellationToken cancellationToken)
    {
        throttle.Reset(user.Username);

        // Activity log is buffered in-request; keep these sequential (WhenAll added no real overlap).
        if (check == PasswordCheck.SuccessRehashNeeded)
        {
            user.ChangePasswordHash(passwordHasher.Hash(password));
            user.RecordLogin(time.GetUtcNow().UtcDateTime);
            await users.SaveChangesAsync(cancellationToken);
            authCache.Invalidate(user.Id);
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
    }

    public async Task<UserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.GetDetailsAsync(userId, cancellationToken);
        return user is { IsActive: true }
            ? new UserDto(user.Id, user.Username, user.Role.ToString(), string.IsNullOrEmpty(user.FullName) ? null : user.FullName)
            : null;
    }

    public Task SignOutAsync(CancellationToken cancellationToken) =>
        activity.LogAsync(new ActivityEntry(ActivityActions.SignedOut, ActivityModules.Auth, "Signed out"), cancellationToken);

    public async Task<LoginResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken) ?? throw AppException.NotFound();
        if (passwordHasher.Verify(user.PasswordHash, request.CurrentPassword) == PasswordCheck.Failed)
        {
            throw AppException.Validation(nameof(ChangePasswordRequest.CurrentPassword), ErrorCodes.WrongPassword);
        }

        user.ChangePasswordHash(passwordHasher.Hash(request.NewPassword));
        await users.SaveChangesAsync(cancellationToken);
        authCache.Invalidate(user.Id);

        await activity.LogAsync(
            new ActivityEntry(ActivityActions.PasswordChanged, ActivityModules.Auth, "Changed own password")
            {
                EntityType = nameof(User),
                EntityId = user.Id.ToString(),
            },
            cancellationToken);

        var token = tokens.CreateAccessToken(user);
        return new LoginResponse(token.Token, token.ExpiresAt, ToDto(user));
    }

    private Task LogFailedSignInAsync(string attemptedUsername, CancellationToken cancellationToken)
    {
        throttle.RecordFailure(attemptedUsername);
        return activity.LogAsync(
            new ActivityEntry(ActivityActions.SignInFailed, ActivityModules.Auth, "Failed sign-in attempt")
            {
                Username = attemptedUsername,
            },
            cancellationToken);
    }

    private string DummyHash() =>
        LazyInitializer.EnsureInitialized(ref _dummyHash, () => passwordHasher.Hash(Guid.NewGuid().ToString()));

    private static UserDto ToDto(User user) =>
        new(user.Id, user.Username, user.Role.ToString(), string.IsNullOrEmpty(user.FullName) ? null : user.FullName);
}
