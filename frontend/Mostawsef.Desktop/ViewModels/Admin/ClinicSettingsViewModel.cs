using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Printing;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Admin;

/// <summary>Admin portal: the clinic's details (shown on printouts and used for money), an edit form and a printer test.</summary>
public partial class ClinicSettingsViewModel(
    ClinicContext clinic,
    ClinicSettingsApi api,
    PrintService print,
    IDialogService dialogs,
    IToastService toasts,
    LocalizationService localization,
    ErrorReporter errors) : ViewModelBase, ILoadablePage
{
    public ClinicContext Clinic => clinic;

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial bool HasLoadError { get; private set; }

    public async Task ActivateAsync()
    {
        IsLoading = true;
        HasLoadError = !await clinic.LoadAsync();
        IsLoading = false;
    }

    public void Deactivate()
    {
    }

    [RelayCommand]
    private Task RefreshAsync() => ActivateAsync();

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

    [RelayCommand]
    private async Task PrintTestPageAsync()
    {
        try
        {
            await print.PrintAsync(new TestPageDocument(print.CreateContext()), "test-page");
        }
        catch (Exception ex)
        {
            errors.Report(ex, context: "Printing the test page");
            toasts.Error(localization.Get("Print.Failed"));
        }
    }
}
