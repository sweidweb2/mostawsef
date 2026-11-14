using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Mostawsef.Contracts.Settings;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Diagnostics;

namespace Mostawsef.Desktop.Services.Local;

/// <summary>
/// The clinic's details (name, address, phone, currency) for money formatting, headers and printouts.
/// Loaded on first use (Admin portal prefetch, print, or Clinic Settings), cleared on sign-out, replaced after an edit.
/// Null until loaded. Concurrent callers share one in-flight GET.
/// </summary>
public partial class ClinicContext : ObservableObject
{
    private readonly ClinicSettingsApi _api;
    private readonly SessionService _session;
    private readonly ErrorReporter _errors;
    /// <summary>Prefetch can race the first portal paint or a flaky start: try this many times, waiting longer each time.</summary>
    private const int MaxAttempts = 3;

    private static readonly TimeSpan RetryDelayStep = TimeSpan.FromMilliseconds(250);

    private readonly object _gate = new();
    private Task<bool>? _inFlight;
    private int _epoch;

    public ClinicContext(ClinicSettingsApi api, SessionService session, ErrorReporter errors)
    {
        _api = api;
        _session = session;
        _errors = errors;
        session.Changed += (_, _) =>
        {
            if (!session.IsAuthenticated)
            {
                lock (_gate)
                {
                    _epoch++;
                    Settings = null;
                }
            }
        };
    }

    [ObservableProperty]
    public partial ClinicSettingsDto? Settings { get; private set; }

    public string? CurrencyCode => Settings?.CurrencyCode;

    /// <summary>
    /// Reads the settings from the API. Skips the GET when a value is already cached unless <paramref name="force"/>
    /// is true (Refresh). Concurrent non-force callers await the same request. Returns false (keeping the last values)
    /// when the call fails. The shared request cannot be cancelled by a caller, so one page leaving cannot starve the others.
    /// </summary>
    public Task<bool> LoadAsync(bool force = false)
    {
        if (!force && Settings is not null)
        {
            return Task.FromResult(true);
        }

        lock (_gate)
        {
            if (!force && Settings is not null)
            {
                return Task.FromResult(true);
            }

            if (!force && _inFlight is { } existing)
            {
                return existing;
            }

            var epoch = _epoch;
            Task<bool>? self = null;
            self = LoadCoreAsync(force, epoch, () => self!);
            return _inFlight = self;
        }
    }

    private async Task<bool> LoadCoreAsync(bool force, int epoch, Func<Task<bool>> self)
    {
        try
        {
            if (!force && Settings is not null)
            {
                return true;
            }

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var result = await _api.GetAsync(CancellationToken.None);
                if (result.IsSuccess)
                {
                    lock (_gate)
                    {
                        if (epoch != _epoch || !_session.IsAuthenticated)
                        {
                            return false;
                        }

                        Settings = result.Value;
                    }

                    return true;
                }

                if (attempt < MaxAttempts)
                {
                    await Task.Delay(RetryDelayStep * attempt);
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _errors.Report(ex, context: "Loading clinic settings");
            return false;
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_inFlight, self()))
                {
                    _inFlight = null;
                }
            }
        }
    }

    public void Update(ClinicSettingsDto settings)
    {
        lock (_gate)
        {
            _epoch++;
            Settings = settings;
        }
    }
}
