using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Settings;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Admin;

/// <summary>Edits the clinic details; a successful save updates ClinicContext for the whole app.</summary>
public partial class ClinicSettingsFormViewModel : FormViewModel
{
    private readonly ClinicSettingsApi _api;
    private readonly ClinicContext _clinic;
    private readonly int _version;

    public ClinicSettingsFormViewModel(
        ClinicSettingsApi api, ClinicContext clinic, ClinicSettingsDto current, IDialogService dialogs, LocalizationService localization)
        : base(dialogs, localization)
    {
        _api = api;
        _clinic = clinic;
        _version = current.Version;

        Name = current.Name;
        Address = current.Address ?? string.Empty;
        Phone = current.Phone ?? string.Empty;
        CurrencyCode = current.CurrencyCode;
        MarkClean();
    }

    public IReadOnlyList<string> CurrencyOptions => Currencies.All;

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Address { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Phone { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrencyCode { get; set; } = Currencies.Egp;

    protected override object BuildRequest() => new UpdateClinicSettingsRequest
    {
        Name = Name.Trim(),
        Address = Address.Trim(),
        Phone = Phone.Trim(),
        CurrencyCode = CurrencyCode,
        Version = _version,
    };

    protected override async Task<IApiResult> SubmitAsync(object request, CancellationToken cancellationToken)
    {
        var result = await _api.UpdateAsync((UpdateClinicSettingsRequest)request, cancellationToken);
        if (result.IsSuccess)
        {
            _clinic.Update(result.Value!);
        }

        return result;
    }
}
