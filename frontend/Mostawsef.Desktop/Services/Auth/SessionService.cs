using System;
using Avalonia.Threading;
using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Services.Api;

namespace Mostawsef.Desktop.Services.Auth;

public enum SignOutReason
{
    None,
    UserRequested,
    Expired,
}

/// <summary>
/// The signed-in user and their access token. Kept in memory only: closing the app signs out.
/// </summary>
public class SessionService
{
    private readonly ApiClient _api;
    private readonly DispatcherTimer _expiryTimer = new();
    private DateTime _expiresAt;

    public SessionService(ApiClient api)
    {
        _api = api;
        _api.Unauthorized += (_, _) => SignOut(SignOutReason.Expired);
        _expiryTimer.Tick += (_, _) => SignOut(SignOutReason.Expired);
    }

    public event EventHandler? Changed;

    public UserDto? CurrentUser { get; private set; }

    /// <summary>The token itself lives only in ApiClient's Authorization header.</summary>
    public bool IsAuthenticated => CurrentUser is not null && DateTime.UtcNow < _expiresAt;

    public SignOutReason LastSignOutReason { get; private set; }

    public bool IsInRole(string role) => IsAuthenticated && string.Equals(CurrentUser!.Role, role, StringComparison.Ordinal);

    public void SignIn(LoginResponse login)
    {
        CurrentUser = login.User;
        _expiresAt = login.ExpiresAt.ToUniversalTime();
        LastSignOutReason = SignOutReason.None;
        _api.SetAccessToken(login.AccessToken);

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
        _expiresAt = default;
        LastSignOutReason = reason;
        _api.SetAccessToken(null);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
