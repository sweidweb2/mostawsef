using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Mostawsef.Contracts.Settings;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Super;

/// <summary>Edits the backup folder and maintenance mode; <see cref="Saved"/> holds the server's answer after a save.</summary>
public partial class SystemSettingsFormViewModel : FormViewModel
{
    private readonly SystemSettingsApi _api;
    private readonly int _version;

    public SystemSettingsFormViewModel(
        SystemSettingsApi api, SystemSettingsDto current, IDialogService dialogs, LocalizationService localization)
        : base(dialogs, localization)
    {
        _api = api;
        _version = current.Version;

        BackupPath = current.BackupPath ?? string.Empty;
        MaintenanceMode = current.MaintenanceMode;
        MarkClean();
    }

    public SystemSettingsDto? Saved { get; private set; }

    [ObservableProperty]
    public partial string BackupPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool MaintenanceMode { get; set; }

    protected override object BuildRequest() => new UpdateSystemSettingsRequest
    {
        BackupPath = BackupPath.Trim(),
        MaintenanceMode = MaintenanceMode,
        Version = _version,
    };

    protected override async Task<IApiResult> SubmitAsync(object request, CancellationToken cancellationToken)
    {
        var result = await _api.UpdateAsync((UpdateSystemSettingsRequest)request, cancellationToken);
        Saved = result.Value;
        return result;
    }
}
