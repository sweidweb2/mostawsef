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
    ISystemSettingsCache systemSettings,
    IPasswordHasher passwordHasher,
    ITokenService tokens,
    IUserAuthCache authCache,
    IActivityLogger activity,
    TimeProvider time) : IAuthService
{
    private static readonly object DummyHashGate = new();
    private static string? _dummyHash;

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var username = User.NormalizeUsername(request.Username);
        var user = await users.GetByUsernameAsync(username, cancellationToken);
        if (user is null || !user.IsActive)
        {
            // Same hashing cost as a real attempt, so response time does not reveal which usernames exist.
            passwordHasher.Verify(DummyHash(passwordHasher), request.Password);
            await LogFailedSignInAsync(username, cancellationToken);
            return null;
        }

        // Overlap the settings read with PBKDF2 for non-Super users (Super skips maintenance).
        var settingsTask = user.Role == UserRole.Super ? null : systemSettings.GetAsync(cancellationToken);

        PasswordCheck check;
        try
        {
            check = passwordHasher.Verify(user.PasswordHash, request.Password);
        }
        catch
        {
            if (settingsTask is not null)
            {
                try
                {
                    await settingsTask;
                }
                catch
                {
                    // Observe so a racing settings fault is not abandoned when Verify fails first.
                }
            }

            throw;
        }

        if (check == PasswordCheck.Failed)
        {
            if (settingsTask is not null)
            {
                await settingsTask;
            }

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

        var signedIn = new ActivityEntry(ActivityActions.SignedIn, ActivityModules.Auth, "Signed in")
        {
            UserId = user.Id,
            Username = user.Username,
            Details = new { Role = user.Role.ToString() },
        };

        // Activity log is buffered in-request; keep these sequential (WhenAll added no real overlap).
        if (check == PasswordCheck.SuccessRehashNeeded)
        {
            user.ChangePasswordHash(passwordHasher.Hash(request.Password));
            user.RecordLogin(time.GetUtcNow().UtcDateTime);
            await users.SaveChangesAsync(cancellationToken);
            authCache.Invalidate(user.Id);
            await activity.LogAsync(signedIn, cancellationToken);
        }
        else
        {
            await users.RecordLoginAsync(user.Id, time.GetUtcNow().UtcDateTime, cancellationToken);
            await activity.LogAsync(signedIn, cancellationToken);
        }

        var token = tokens.CreateAccessToken(user);
        return new LoginResponse(token.Token, token.ExpiresAt, ToDto(user));
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

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
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
    }

    private Task LogFailedSignInAsync(string attemptedUsername, CancellationToken cancellationToken) =>
        activity.LogAsync(
            new ActivityEntry(ActivityActions.SignInFailed, ActivityModules.Auth, "Failed sign-in attempt")
            {
                Username = attemptedUsername,
            },
            cancellationToken);

    private static string DummyHash(IPasswordHasher passwordHasher)
    {
        lock (DummyHashGate)
        {
            return _dummyHash ??= passwordHasher.Hash(Guid.NewGuid().ToString());
        }
    }

    private static UserDto ToDto(User user) =>
        new(user.Id, user.Username, user.Role.ToString(), string.IsNullOrEmpty(user.FullName) ? null : user.FullName);
}
