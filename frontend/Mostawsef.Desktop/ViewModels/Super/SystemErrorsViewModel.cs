using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Super;

/// <summary>Super portal: system errors with stats, paging, search, details modal, delete and bulk delete. Access via AppRoutes.</summary>
public partial class SystemErrorsViewModel(LogsApi logs, IDialogService dialogs, IToastService toasts, LocalizationService localization, ErrorReporter errors)
    : SelectableLogListViewModel<SystemErrorListItemDto>(dialogs, toasts, localization, errors)
{
    protected override string DeleteAllConfirmKey => "Logs.DeleteAllErrorsConfirm";

    protected override Guid IdOf(SystemErrorListItemDto item) => item.Id;

    protected override Task<ApiResult<DeleteLogsResultDto>> DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        logs.DeleteSystemErrorsAsync(ids, cancellationToken);

    protected override Task<ApiResult<bool>> DeleteEverythingAsync(CancellationToken cancellationToken) =>
        logs.DeleteAllSystemErrorsAsync(cancellationToken);

    [ObservableProperty]
    public partial SystemErrorStatsDto? Stats { get; private set; }

    protected override int? ExtrasTotalCount => Stats?.Total;

    protected override Task<ApiResult<CursorPage<SystemErrorListItemDto>>> FetchPageAsync(CursorQuery query, CancellationToken cancellationToken) =>
        logs.GetSystemErrorsAsync(query, cancellationToken);

    protected override async Task LoadExtrasAsync(CancellationToken cancellationToken)
    {
        var stats = await logs.GetErrorStatsAsync(cancellationToken);
        if (stats.IsSuccess)
        {
            Stats = stats.Value;
        }
    }

    [RelayCommand]
    private async Task ViewDetailsAsync(SystemErrorListItemDto? item)
    {
        if (item is null)
        {
            return;
        }

        var result = await ReadAsync(cancellationToken => logs.GetSystemErrorAsync(item.Id, cancellationToken));
        if (result is null)
        {
            return;
        }

        if (!result.IsSuccess)
        {
            ShowError(result.Error);
            return;
        }

        await Dialogs.ShowAsync(new Modal
        {
            Title = Localization.Get("Logs.ErrorDetails"),
            Content = new SystemErrorDetailsViewModel(result.Value!),
            MaxWidth = 820,
        });
    }

    [RelayCommand]
    private async Task DeleteAsync(SystemErrorListItemDto? item)
    {
        if (item is null
            || !await Dialogs.ConfirmAsync(Localization.Get("Logs.DeleteErrorConfirm"), confirmText: Localization.Get("Common.Delete"), isDanger: true))
        {
            return;
        }

        if (await ReadAsync(ct => logs.DeleteSystemErrorAsync(item.Id, ct)) is not { } result)
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
