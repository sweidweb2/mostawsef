using Microsoft.AspNetCore.Identity;
using Mostawsef.Application.Interfaces;

namespace Mostawsef.Infrastructure.Services.Auth;

/// <summary>
/// ASP.NET Core Identity password hashing: PBKDF2 with HMAC-SHA512, 100,000 iterations,
/// a random 128-bit salt per password. The salt and parameters are stored inside the hash string.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _inner = new();
    private static readonly object User = new();

    public string Hash(string password) => _inner.HashPassword(User, password);

    public PasswordCheck Verify(string passwordHash, string password) =>
        _inner.VerifyHashedPassword(User, passwordHash, password) switch
        {
            PasswordVerificationResult.Success => PasswordCheck.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
            _ => PasswordCheck.Failed,
        };
}
