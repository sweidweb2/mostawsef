using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Mostawsef.Contracts.Settings;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Auth;

namespace Mostawsef.Desktop.Services.Local;

/// <summary>
/// The clinic's details (name, address, phone, currency) for money formatting, headers and printouts.
/// Loaded when a portal opens, cleared on sign-out, replaced after an edit. Null until loaded.
/// </summary>
public partial class ClinicContext : ObservableObject
{
    private readonly ClinicSettingsApi _api;

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

    /// <summary>Reads the settings from the API. Returns false (keeping the last values) when that fails.</summary>
    public async Task<bool> LoadAsync(CancellationToken cancellationToken = default)
    {
        var result = await _api.GetAsync(cancellationToken);
        if (result.IsSuccess)
        {
            Settings = result.Value;
        }

        return result.IsSuccess;
    }

    public void Update(ClinicSettingsDto settings) => Settings = settings;
}
