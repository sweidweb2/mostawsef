using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Mostawsef.Contracts.Settings;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Auth;

namespace Mostawsef.Desktop.Services.Local;

/// <summary>
/// The clinic's details (name, address, phone, currency) for money formatting, headers and printouts.
/// Loaded on first use (Admin portal prefetch, print, or Clinic Settings), cleared on sign-out, replaced after an edit.
/// Null until loaded. Concurrent callers share one in-flight GET.
/// </summary>
public partial class ClinicContext : ObservableObject
{
    private readonly ClinicSettingsApi _api;
    private readonly object _gate = new();
    private Task<bool>? _inFlight;

    public ClinicContext(ClinicSettingsApi api, SessionService session)
    {
        _api = api;
        session.Changed += (_, _) =>
        {
            if (!session.IsAuthenticated)
            {
                Settings = null;
            }
        };
    }

    [ObservableProperty]
    public partial ClinicSettingsDto? Settings { get; private set; }

    public string? CurrencyCode => Settings?.CurrencyCode;

    /// <summary>
    /// Reads the settings from the API. Skips the GET when a value is already cached unless <paramref name="force"/>
    /// is true (Refresh). Concurrent non-force callers await the same request. Returns false (keeping the last values)
    /// when the call fails.
    /// </summary>
    public Task<bool> LoadAsync(bool force = false, CancellationToken cancellationToken = default)
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

            return _inFlight = LoadCoreAsync(force, cancellationToken);
        }
    }

    private async Task<bool> LoadCoreAsync(bool force, CancellationToken cancellationToken)
    {
        try
        {
            if (!force && Settings is not null)
            {
                return true;
            }

            var result = await _api.GetAsync(cancellationToken);
            if (result.IsSuccess)
            {
                Settings = result.Value;
            }

            return result.IsSuccess;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            lock (_gate)
            {
                _inFlight = null;
            }
        }
    }

    public void Update(ClinicSettingsDto settings) => Settings = settings;
}
