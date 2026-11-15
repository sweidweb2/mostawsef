using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Maintenance;
using Mostawsef.Contracts.Settings;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Super;

/// <summary>Super portal: the backup folder and maintenance mode, with an edit form, and the data reset (danger zone).</summary>
public partial class SystemSettingsViewModel(
    SystemSettingsApi api,
    SystemApi system,
    IDialogService dialogs,
    IToastService toasts,
    LocalizationService localization,
    FolderPathPicker pickFolder) : ViewModelBase, ILoadablePage
{
    private bool _loaded;
    private CancellationTokenSource? _loadCancellation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSettings), nameof(MaintenanceModeKey), nameof(MaintenanceNoticeKey))]
    public partial SystemSettingsDto? Settings { get; private set; }

    public bool HasSettings => Settings is not null;

    public string? MaintenanceModeKey => Settings is null ? null : Settings.MaintenanceMode ? "SystemSettings.On" : "SystemSettings.Off";

    /// <summary>Shown as a warning while maintenance mode is on.</summary>
    public string? MaintenanceNoticeKey => Settings?.MaintenanceMode == true ? "SystemSettings.MaintenanceActive" : null;

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial bool HasLoadError { get; private set; }

    /// <summary>A data reset is running: its button is disabled.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetDataCommand))]
    public partial bool IsResetting { get; private set; }

    public Task ActivateAsync() => _loaded ? Task.CompletedTask : LoadAsync();

    public void Deactivate()
    {
        if (_loadCancellation is not null)
        {
            _loaded = false;
        }

        _loadCancellation?.Cancel();
        IsLoading = false;
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task EditAsync()
    {
        if (Settings is not { } settings)
        {
            return;
        }

        var form = new SystemSettingsFormViewModel(api, settings, dialogs, localization, pickFolder);
        if (await dialogs.ShowFormAsync(form, localization.Get("SystemSettings.EditTitle")))
        {
            Settings = form.Saved;
            toasts.Success(localization.Get("SystemSettings.Saved"));
        }
    }

    /// <summary>
    /// Deletes every patient, order, payment, lab test and log record; accounts and settings stay. The user must type
    /// <see cref="DataResetRules.ConfirmationWord"/> in the confirmation dialog, and the API checks the word again.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanResetData))]
    private async Task ResetDataAsync()
    {
        if (!await dialogs.ConfirmWithWordAsync(
                localization.Get("SystemSettings.ResetConfirm"),
                DataResetRules.ConfirmationWord,
                localization.Get("SystemSettings.ResetTitle"),
                localization.Get("SystemSettings.ResetButton")))
        {
            return;
        }

        IsResetting = true;
        try
        {
            var result = await system.ResetDataAsync(DataResetRules.ConfirmationWord);
            if (result.IsSuccess)
            {
                var removed = result.Value!;
                toasts.Success(localization.Format(
                    "SystemSettings.ResetDone",
                    removed.Patients, removed.Orders, removed.LabTests, removed.ActivityLogs + removed.SystemErrors));
            }
            else
            {
                toasts.Error(localization.Get(
                    result.Error == ApiError.Unreachable ? "Login.ServerUnreachable" : "SystemSettings.ResetFailed"));
            }
        }
        finally
        {
            IsResetting = false;
        }
    }

    private bool CanResetData() => !IsResetting;

    private async Task LoadAsync()
    {
        _loadCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;

        IsLoading = true;
        try
        {
            var result = await api.GetAsync(cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            Settings = result.Value ?? Settings;
            HasLoadError = !result.IsSuccess;
            _loaded = result.IsSuccess;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // The user left the page; the next activation loads again when the interrupted load marked it stale.
        }
        finally
        {
            if (_loadCancellation == cancellation)
            {
                _loadCancellation = null;
                IsLoading = false;
            }
        }
    }
}
