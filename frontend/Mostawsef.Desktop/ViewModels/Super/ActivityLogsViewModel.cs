using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Super;

/// <summary>Super portal: activity log with stats, paging, search, details modal and delete. Access via AppRoutes.</summary>
public partial class ActivityLogsViewModel(LogsApi logs, IDialogService dialogs, IToastService toasts, LocalizationService localization)
    : PagedListViewModel<ActivityLogListItemDto>(toasts, localization)
{
    [ObservableProperty]
    public partial ActivityLogStatsDto? Stats { get; private set; }

    protected override int? ExtrasTotalCount => Stats?.Total;

    protected override Task<ApiResult<CursorPage<ActivityLogListItemDto>>> FetchPageAsync(CursorQuery query, CancellationToken cancellationToken) =>
        logs.GetActivityLogsAsync(query, cancellationToken);

    protected override async Task LoadExtrasAsync(CancellationToken cancellationToken)
    {
        var stats = await logs.GetActivityStatsAsync(cancellationToken);
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

        var result = await ReadAsync(cancellationToken => logs.GetActivityLogAsync(item.Id, cancellationToken));
        if (result is null)
        {
            return;
        }

        if (!result.IsSuccess)
        {
            ShowError(result.Error);
            return;
        }

        await dialogs.ShowAsync(new Modal
        {
            Title = Localization.Get("Logs.ActivityDetails"),
            Content = new ActivityLogDetailsViewModel(result.Value!),
            MaxWidth = 720,
        });
    }

    [RelayCommand]
    private async Task DeleteAsync(ActivityLogListItemDto? item)
    {
        if (item is null
            || !await dialogs.ConfirmAsync(Localization.Get("Logs.DeleteActivityConfirm"), confirmText: Localization.Get("Common.Delete"), isDanger: true))
        {
            return;
        }

        if (await ReadAsync(ct => logs.DeleteActivityLogAsync(item.Id, ct)) is not { } result)
        {
            return;
        }

        if (!result.IsSuccess)
        {
            ShowError(result.Error);
            return;
        }

        Toasts.Success(Localization.Get("Logs.Deleted"));
        await ReloadAsync();
    }
}
