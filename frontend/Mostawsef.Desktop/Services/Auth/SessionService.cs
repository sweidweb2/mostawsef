using System;
using Avalonia.Threading;
using Mostawsef.Contracts.Auth;

namespace Mostawsef.Desktop.Services.Auth;

public enum SignOutReason
{
    None,
    UserRequested,
    Expired,
}

/// <summary>
/// The signed-in user and their access token. Kept in memory only: closing the app signs out.
/// AuthHeaderHandler adds the token to API requests.
/// </summary>
public class SessionService
{
    private readonly DispatcherTimer _expiryTimer = new();
    private string? _accessToken;
    private DateTime _expiresAt;

    public SessionService()
    {
        _expiryTimer.Tick += (_, _) => SignOut(SignOutReason.Expired);
    }

    public event EventHandler? Changed;

    public UserDto? CurrentUser { get; private set; }

    /// <summary>Bearer token of the current session, or null when signed out or expired.</summary>
    public string? AccessToken => IsAuthenticated ? _accessToken : null;

    public bool IsAuthenticated => CurrentUser is not null && DateTime.UtcNow < _expiresAt;

    public SignOutReason LastSignOutReason { get; private set; }

    public bool IsInRole(string role) => IsAuthenticated && string.Equals(CurrentUser!.Role, role, StringComparison.Ordinal);

    public void SignIn(LoginResponse login)
    {
        CurrentUser = login.User;
        _accessToken = login.AccessToken;
        _expiresAt = login.ExpiresAt.ToUniversalTime();
        LastSignOutReason = SignOutReason.None;

        _expiryTimer.Stop();
        var remaining = _expiresAt - DateTime.UtcNow;
        _expiryTimer.Interval = remaining > TimeSpan.Zero ? remaining : TimeSpan.FromSeconds(1);
        _expiryTimer.Start();

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SignOut(SignOutReason reason = SignOutReason.UserRequested)
    {
        if (CurrentUser is null)
        {
            return;
        }

        _expiryTimer.Stop();
        CurrentUser = null;
        _accessToken = null;
        _expiresAt = default;
        LastSignOutReason = reason;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Signs out as expired if <paramref name="rejectedToken"/> is still the current token
    /// (a late 401 for an earlier session is ignored). Safe to call from any thread.
    /// </summary>
    public void Expire(string rejectedToken)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Expire(rejectedToken));
            return;
        }

        if (string.Equals(_accessToken, rejectedToken, StringComparison.Ordinal))
        {
            SignOut(SignOutReason.Expired);
        }
    }
}
