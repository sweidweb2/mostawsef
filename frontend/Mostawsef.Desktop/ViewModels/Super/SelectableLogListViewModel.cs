using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Logs;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Super;

/// <summary>
/// A log list (activity logs, system errors) with a select mode: "Select" shows a checkbox per row and a "select this
/// page" box; the selection is kept across pages and can be deleted in one go. "Delete all" empties the whole log.
/// Subclasses name the row id and the two delete calls.
/// </summary>
public abstract partial class SelectableLogListViewModel<TItem> : PagedListViewModel<TItem>, ISelectableList
{
    protected SelectableLogListViewModel(IDialogService dialogs, IToastService toasts, LocalizationService localization, ErrorReporter errors)
        : base(toasts, localization, errors)
    {
        Dialogs = dialogs;
        Selection.PropertyChanged += OnSelectionChanged;
        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsPageSelected));
    }

    protected IDialogService Dialogs { get; }

    public RowSelection Selection { get; } = new();

    /// <summary>The label of the select-mode button: "Select" or "Cancel".</summary>
    public string SelectModeKey => Selection.IsActive ? "Common.Cancel" : "Logs.Select";

    /// <summary>A bulk delete is running: the bulk buttons are disabled.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand), nameof(DeleteAllCommand))]
    public partial bool IsDeleting { get; private set; }

    /// <summary>"Select this page": checked when every row on the shown page is selected; setting it (un)selects them all.</summary>
    public bool IsPageSelected
    {
        get => Items.Count > 0 && Items.All(i => Selection.Contains(IdOf(i)));
        set => Selection.Set(Items.Select(IdOf).ToList(), value);
    }

    protected abstract Guid IdOf(TItem item);

    /// <summary>Deletes these records (at most <see cref="LogRules.MaxBulkDelete"/>); the value is how many went.</summary>
    protected abstract Task<ApiResult<DeleteLogsResultDto>> DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);

    protected abstract Task<ApiResult<bool>> DeleteEverythingAsync(CancellationToken cancellationToken);

    /// <summary>The confirmation text of "Delete all".</summary>
    protected abstract string DeleteAllConfirmKey { get; }

    [RelayCommand]
    private void ToggleSelectMode() => Selection.IsActive = !Selection.IsActive;

    [RelayCommand]
    private void ToggleRow(TItem? item)
    {
        if (item is not null)
        {
            Selection.Toggle(IdOf(item));
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private async Task DeleteSelectedAsync()
    {
        var ids = Selection.Ids;
        if (ids.Count == 0
            || !await Dialogs.ConfirmAsync(
                Localization.Format("Logs.DeleteSelectedConfirm", ids.Count),
                confirmText: Localization.Get("Common.Delete"),
                isDanger: true))
        {
            return;
        }

        IsDeleting = true;
        try
        {
            var deleted = 0;
            foreach (var chunk in ids.Chunk(LogRules.MaxBulkDelete))
            {
                if (await ReadAsync(ct => DeleteManyAsync(chunk, ct)) is not { } result)
                {
                    return;
                }

                if (!result.IsSuccess)
                {
                    ShowError(result);
                    break;
                }

                deleted += result.Value!.Deleted;
            }

            if (deleted > 0)
            {
                Toasts.Success(Localization.Format("Logs.DeletedMany", deleted));
            }

            Selection.IsActive = false;
            await ReloadAsync();
        }
        finally
        {
            IsDeleting = false;
        }
    }

    private bool CanDeleteSelected() => !IsDeleting && Selection.HasAny;

    [RelayCommand(CanExecute = nameof(CanDeleteAll))]
    private async Task DeleteAllAsync()
    {
        if (!await Dialogs.ConfirmAsync(
                Localization.Get(DeleteAllConfirmKey),
                title: Localization.Get("Logs.DeleteAll"),
                confirmText: Localization.Get("Logs.DeleteAll"),
                isDanger: true))
        {
            return;
        }

        IsDeleting = true;
        try
        {
            if (await ReadAsync(DeleteEverythingAsync) is not { } result)
            {
                return;
            }

            if (!result.IsSuccess)
            {
                ShowError(result);
                return;
            }

            Toasts.Success(Localization.Get("Logs.DeletedAll"));
            Selection.IsActive = false;
            await ReloadAsync();
        }
        finally
        {
            IsDeleting = false;
        }
    }

    private bool CanDeleteAll() => !IsDeleting;

    private void OnSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RowSelection.IsActive))
        {
            OnPropertyChanged(nameof(SelectModeKey));
        }

        if (e.PropertyName == nameof(RowSelection.Version))
        {
            OnPropertyChanged(nameof(IsPageSelected));
            DeleteSelectedCommand.NotifyCanExecuteChanged();
        }
    }
}
