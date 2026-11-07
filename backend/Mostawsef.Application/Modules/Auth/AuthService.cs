using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Auth;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Modules.Auth;

public interface IAuthService
{
    /// <summary>Returns null when the credentials are invalid or the user is inactive.</summary>
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<UserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
}

public class AuthService(IUserRepository users, IPasswordHasher passwordHasher, ITokenService tokens) : IAuthService
{
    private static string? _dummyHash;

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetByUsernameAsync(User.NormalizeUsername(request.Username), cancellationToken);
        if (user is null || !user.IsActive)
        {
            // Same hashing cost as a real attempt, so response time does not reveal which usernames exist.
            passwordHasher.Verify(_dummyHash ??= passwordHasher.Hash(Guid.NewGuid().ToString()), request.Password);
            return null;
        }

        var check = passwordHasher.Verify(user.PasswordHash, request.Password);
        if (check == PasswordCheck.Failed)
        {
            return null;
        }

        if (check == PasswordCheck.SuccessRehashNeeded)
        {
            user.ChangePasswordHash(passwordHasher.Hash(request.Password));
        }

        user.RecordLogin();
        await users.SaveChangesAsync(cancellationToken);

        var token = tokens.CreateAccessToken(user);
        return new LoginResponse(token.Token, token.ExpiresAt, ToDto(user));
    }

    public async Task<UserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        return user is { IsActive: true } ? ToDto(user) : null;
    }

    private static UserDto ToDto(User user) => new(user.Id, user.Username, user.Role.ToString());
}
