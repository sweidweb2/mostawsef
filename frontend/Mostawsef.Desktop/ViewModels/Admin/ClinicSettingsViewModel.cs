using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Admin;

/// <summary>Admin portal: the clinic's details (shown on printouts and used for money) and an edit form.</summary>
public partial class ClinicSettingsViewModel(
    ClinicContext clinic,
    ClinicSettingsApi api,
    IDialogService dialogs,
    IToastService toasts,
    LocalizationService localization) : ViewModelBase, ILoadablePage
{
    private bool _loaded;

    public ClinicContext Clinic => clinic;

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial bool HasLoadError { get; private set; }

    public async Task ActivateAsync()
    {
        if (_loaded)
        {
            return;
        }

        // Portal already starts loading clinic settings; reuse them when present.
        if (clinic.Settings is not null)
        {
            _loaded = true;
            return;
        }

        await LoadAsync();
    }

    public void Deactivate()
    {
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task EditAsync()
    {
        if (clinic.Settings is { } settings
            && await dialogs.ShowFormAsync(
                new ClinicSettingsFormViewModel(api, clinic, settings, dialogs, localization), localization.Get("Settings.EditTitle")))
        {
            toasts.Success(localization.Get("Settings.Saved"));
        }
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        HasLoadError = !await clinic.LoadAsync();
        _loaded = clinic.Settings is not null;
        IsLoading = false;
    }
}
