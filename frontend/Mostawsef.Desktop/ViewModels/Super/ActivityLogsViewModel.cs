using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Super;

[RequiresRole(Roles.Super)]
public partial class ActivityLogsViewModel : PagedListViewModel<ActivityLogListItemDto>
{
    private readonly LogsApi _logs;
    private readonly IDialogService _dialogs;

    public ActivityLogsViewModel(LogsApi logs, IDialogService dialogs, IToastService toasts)
        : base(toasts)
    {
        _logs = logs;
        _dialogs = dialogs;
    }

    [ObservableProperty]
    public partial ActivityLogStatsDto? Stats { get; private set; }

    protected override Task<ApiResult<PagedResult<ActivityLogListItemDto>>> FetchPageAsync(int page, int pageSize, string? search) =>
        _logs.GetActivityLogsAsync(page, pageSize, search);

    protected override async Task LoadExtrasAsync()
    {
        var stats = await _logs.GetActivityStatsAsync();
        if (stats.IsSuccess)
        {
            Stats = stats.Value;
        }
    }

    [RelayCommand]
    private async Task ViewDetailsAsync(ActivityLogListItemDto? item)
    {
        if (item is null)
        {
            return;
        }

        var result = await _logs.GetActivityLogAsync(item.Id);
        if (!result.IsSuccess)
        {
            ShowError(result.Error);
            return;
        }

        var modal = new Modal
        {
            Title = LocalizationService.Instance.Get("Logs.ActivityDetails"),
            Content = new ActivityLogDetailsViewModel(result.Value!),
            MaxWidth = 720,
        };
        await _dialogs.ShowAsync(modal);
    }

    [RelayCommand]
    private async Task DeleteAsync(ActivityLogListItemDto? item)
    {
        if (item is null)
        {
            return;
        }

        var loc = LocalizationService.Instance;
        if (!await _dialogs.ConfirmAsync(loc.Get("Logs.DeleteActivityConfirm"), confirmText: loc.Get("Common.Delete"), isDanger: true))
        {
            return;
        }

        var result = await _logs.DeleteActivityLogAsync(item.Id);
        if (!result.IsSuccess)
        {
            ShowError(result.Error);
            return;
        }

        Toasts.Success(loc.Get("Logs.Deleted"));
        await LoadAsync(resetPage: false);
    }
}
