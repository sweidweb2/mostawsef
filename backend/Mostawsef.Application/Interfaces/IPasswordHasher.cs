namespace Mostawsef.Application.Interfaces;

public enum PasswordCheck
{
    Failed,
    Success,

    /// <summary>Correct password, but the hash uses outdated parameters and should be replaced.</summary>
    SuccessRehashNeeded,
}

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordCheck Verify(string passwordHash, string password);
}
