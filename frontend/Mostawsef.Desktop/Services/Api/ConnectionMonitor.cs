using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Mostawsef.Contracts.Common;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Auth;

namespace Mostawsef.Desktop.Services.Api;

public enum ConnectionState
{
    Online,

    /// <summary>The API cannot be reached, or it answers that it is not healthy (e.g. its database is down).</summary>
    Offline,

    /// <summary>The API was built for another desktop version (ApiVersion differs).</summary>
    VersionMismatch,
}

/// <summary>
/// Watches the API while someone is signed in: checks GET /api/health every 15 s (every 3 s while offline),
/// and goes offline at once when any API call cannot reach the server. The portal shell shows the banner it describes.
/// </summary>
public partial class ConnectionMonitor : ObservableObject
{
    public static readonly TimeSpan OnlineInterval = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan OfflineInterval = TimeSpan.FromSeconds(3);

    private readonly ApiClient _api;
    private readonly DispatcherTimer _timer = new();

    public ConnectionMonitor(ApiClient api, SessionService session)
    {
        _api = api;
        _timer.Tick += async (_, _) => await CheckNowAsync();
        api.Unreachable += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (IsRunning)
            {
                State = ConnectionState.Offline;
            }
        });
        session.Changed += (_, _) =>
        {
            if (!session.IsAuthenticated)
            {
                Stop();
            }
        };
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MessageKey), nameof(AlertKind))]
    public partial ConnectionState State { get; private set; }

    public bool IsRunning { get; private set; }

    /// <summary>Resource key of the banner text, or null when online (no banner).</summary>
    public string? MessageKey => State switch
    {
        ConnectionState.Offline => "Connection.Lost",
        ConnectionState.VersionMismatch => "Connection.VersionMismatch",
        _ => null,
    };

    public BadgeKind AlertKind => State == ConnectionState.VersionMismatch ? BadgeKind.Danger : BadgeKind.Warning;

    /// <summary>Starts watching (idempotent) with an immediate check.</summary>
    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        _ = CheckNowAsync();
    }

    public void Stop()
    {
        IsRunning = false;
        _timer.Stop();
        State = ConnectionState.Online;
    }

    public async Task CheckNowAsync()
    {
        _timer.Stop();
        var result = await _api.GetAsync<HealthDto>("api/health");
        if (!IsRunning)
        {
            return;
        }

        State = result switch
        {
            { IsSuccess: true } => result.Value!.ApiVersion == ApiVersion.Current ? ConnectionState.Online : ConnectionState.VersionMismatch,
            { Error: ApiError.Unreachable or ApiError.Server } => ConnectionState.Offline,
            _ => ConnectionState.Online,
        };

        _timer.Interval = State == ConnectionState.Offline ? OfflineInterval : OnlineInterval;
        _timer.Start();
    }

    partial void OnStateChanged(ConnectionState value)
    {
        if (value == ConnectionState.Offline && IsRunning && _timer.Interval != OfflineInterval)
        {
            _timer.Stop();
            _timer.Interval = OfflineInterval;
            _timer.Start();
        }
    }
}
