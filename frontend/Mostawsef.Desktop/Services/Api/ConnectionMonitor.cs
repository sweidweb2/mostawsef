using System;
using System.Threading;
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
/// Watches the API while someone is signed in: checks GET /api/health every 60 s (every 3 s at the start of an
/// outage, backing off to 60 s), and goes offline at once when any API call cannot reach the server.
/// The portal shell shows the banner it describes. Stop cancels any in-flight check.
/// </summary>
public partial class ConnectionMonitor : ObservableObject
{
    public static readonly TimeSpan OnlineInterval = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan OfflineInterval = TimeSpan.FromSeconds(3);

    /// <summary>After this many consecutive offline checks, poll at <see cref="OnlineInterval"/> instead of 3 s.</summary>
    public const int FastOfflineChecks = 5;

    private readonly ApiClient _api;
    private readonly DispatcherTimer _timer = new();
    private CancellationTokenSource? _checkCancellation;
    private int _consecutiveOffline;

    public ConnectionMonitor(ApiClient api, SessionService session)
    {
        _api = api;
        _timer.Tick += (_, _) => _ = CheckNowAsync();
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
        _consecutiveOffline = 0;
        _ = CheckNowAsync();
    }

    public void Stop()
    {
        IsRunning = false;
        _timer.Stop();
        _checkCancellation?.Cancel();
        _checkCancellation = null;
        _consecutiveOffline = 0;
        State = ConnectionState.Online;
    }

    public async Task CheckNowAsync()
    {
        _timer.Stop();
        _checkCancellation?.Cancel();
        var check = new CancellationTokenSource();
        _checkCancellation = check;
        var cancellationToken = check.Token;

        try
        {
            var result = await _api.GetAsync<HealthDto>("api/health", cancellationToken);
            if (!IsRunning || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            State = result switch
            {
                { IsSuccess: true } => result.Value!.ApiVersion == ApiVersion.Current ? ConnectionState.Online : ConnectionState.VersionMismatch,
                { Error: ApiError.Unreachable or ApiError.Server } => ConnectionState.Offline,
                _ => ConnectionState.Online,
            };

            if (State == ConnectionState.Offline)
            {
                _consecutiveOffline++;
            }
            else
            {
                _consecutiveOffline = 0;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stopped or replaced by a newer check.
        }
        catch (Exception)
        {
            if (IsRunning && !cancellationToken.IsCancellationRequested)
            {
                State = ConnectionState.Offline;
                _consecutiveOffline++;
            }
        }
        finally
        {
            if (_checkCancellation == check)
            {
                _checkCancellation = null;
            }

            check.Dispose();
            if (IsRunning && !cancellationToken.IsCancellationRequested)
            {
                ScheduleNext();
            }
        }
    }

    private void ScheduleNext()
    {
        if (!IsRunning)
        {
            return;
        }

        _timer.Interval = State == ConnectionState.Offline && _consecutiveOffline <= FastOfflineChecks
            ? OfflineInterval
            : OnlineInterval;
        _timer.Start();
    }

    partial void OnStateChanged(ConnectionState value)
    {
        if (value == ConnectionState.Offline && IsRunning && _timer.IsEnabled
            && _timer.Interval != OfflineInterval && _consecutiveOffline <= FastOfflineChecks)
        {
            _timer.Stop();
            _timer.Interval = OfflineInterval;
            _timer.Start();
        }
    }
}
