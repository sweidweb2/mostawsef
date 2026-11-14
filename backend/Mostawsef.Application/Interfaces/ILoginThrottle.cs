namespace Mostawsef.Application.Interfaces;

/// <summary>
/// Limits failed sign-ins per username, on top of the per-IP rate limit, so a password guess cannot be spread
/// over many IPs. A username that failed too often is blocked for the rest of the window; a successful sign-in clears it.
/// Every submitted username is counted the same way, so the limit does not reveal which accounts exist.
/// </summary>
public interface ILoginThrottle
{
    bool IsBlocked(string username);

    void RecordFailure(string username);

    void Reset(string username);
}
