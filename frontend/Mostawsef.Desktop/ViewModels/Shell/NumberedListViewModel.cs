using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Common;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;

namespace Mostawsef.Desktop.ViewModels.Shell;

/// <summary>
/// List pages paged by page number (PageQuery / PagedResult): records sorted by name or another field, e.g. users.
/// Same bindings as <see cref="PagedListViewModel{TItem}"/>, which is for append-only, newest-first logs (cursor paging);
/// the shared state and lifecycle live in <see cref="ListPageViewModelBase{TItem}"/>.
/// Page flips skip the filtered total; search and refresh ask for it again.
/// </summary>
public abstract partial class NumberedListViewModel<TItem> : ListPageViewModelBase<TItem>
{
    protected NumberedListViewModel(IToastService toasts, LocalizationService localization, ErrorReporter errors)
        : base(toasts, localization, errors)
    {
    }

    protected override Task LoadOnActivateAsync() => ReloadAsync();

    protected override void OnPageSelected(int page) => _ = LoadAsync(page, includeTotal: false);

    /// <summary>
    /// Puts a newly created row on page 1 without a server reload. Marks the page stale when a search is active
    /// or the user is not on page 1 (the new row may not belong on the screen).
    /// </summary>
    protected void PrependNewest(TItem item)
    {
        if (!IsLoaded || CurrentPage != 1 || HasActiveFilter)
        {
            MarkStale();
            return;
        }

        LoadError = null;
        Items.Insert(0, item);
        TrimToPageSize();
        BumpTotalAfterInsert();
    }

    /// <summary>
    /// Inserts a newly created row in list sort order on page 1 without a reload.
    /// Marks stale when a search is active or the user is not on page 1.
    /// When the page is full and the row sorts after the last visible item, only the total is bumped.
    /// </summary>
    protected void InsertSorted(TItem item, Comparison<TItem> compare)
    {
        if (!IsLoaded || CurrentPage != 1 || HasActiveFilter)
        {
            MarkStale();
            return;
        }

        LoadError = null;
        var index = 0;
        while (index < Items.Count && compare(Items[index], item) <= 0)
        {
            index++;
        }

        if (index >= PageSize)
        {
            BumpTotalAfterInsert();
            return;
        }

        Items.Insert(index, item);
        TrimToPageSize();
        BumpTotalAfterInsert();
    }

    /// <summary>
    /// Replaces an edited row already on the current page, keeping list sort order, without a reload.
    /// Marks stale when a search is active or the row is not on this page (or sorts off it after the edit).
    /// </summary>
    protected void ReplaceSorted(TItem item, Func<TItem, Guid> idOf, Comparison<TItem> compare)
    {
        if (!IsLoaded || HasActiveFilter)
        {
            MarkStale();
            return;
        }

        for (var i = 0; i < Items.Count; i++)
        {
            if (idOf(Items[i]) != idOf(item))
            {
                continue;
            }

            LoadError = null;
            Items.RemoveAt(i);
            var index = 0;
            while (index < Items.Count && compare(Items[index], item) <= 0)
            {
                index++;
            }

            if (index >= PageSize)
            {
                MarkStale();
                OnPropertyChanged(nameof(IsEmpty));
                return;
            }

            Items.Insert(index, item);
            OnPropertyChanged(nameof(IsEmpty));
            return;
        }

        MarkStale();
    }

    /// <summary>
    /// Takes a deleted row off the page. When no rows follow this page, that is all (no server call); when rows do
    /// follow, only this page is fetched again to close the gap, without re-counting the total. A capped total
    /// ("10,000+") cannot be lowered by one, and a row not on the page is unknown, so those reload as before.
    /// </summary>
    protected Task RemoveDeletedAsync(Guid id, Func<TItem, Guid> idOf)
    {
        var index = -1;
        for (var i = 0; i < Items.Count; i++)
        {
            if (idOf(Items[i]) == id)
            {
                index = i;
                break;
            }
        }

        if (!IsLoaded || index < 0 || PageQuery.IsTotalCapped(TotalCount) || TotalCount <= 0)
        {
            return ReloadAsync();
        }

        var rowsFollow = CurrentPage * PageSize < TotalCount;
        TotalCount--;
        PageCount = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        if (rowsFollow)
        {
            return ReloadAsync(includeTotal: false);
        }

        Items.RemoveAt(index);
        if (Items.Count == 0 && CurrentPage > 1)
        {
            return LoadAsync(CurrentPage - 1, includeTotal: false);
        }

        OnPropertyChanged(nameof(IsEmpty));
        return Task.CompletedTask;
    }

    /// <summary>True while a search (or a subclass's own filter) may hide a new row; override to add filters.</summary>
    protected virtual bool HasActiveFilter => !string.IsNullOrWhiteSpace(_appliedSearch);

    /// <summary>A subclass's own filter changed: show page 1 of the matching rows and count them again.</summary>
    protected Task ApplyFilterAsync() => LoadAsync(page: 1, includeTotal: true);

    private void TrimToPageSize()
    {
        while (Items.Count > PageSize)
        {
            Items.RemoveAt(Items.Count - 1);
        }
    }

    private void BumpTotalAfterInsert()
    {
        TotalCount++;
        PageCount = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync(CurrentPage, includeTotal: true);
        await OnAfterRefreshAsync();
    }

    /// <summary>Hook for a page that hosts another list (e.g. Backups also refreshes dump history).</summary>
    protected virtual Task OnAfterRefreshAsync() => Task.CompletedTask;

    /// <summary>
    /// A new filter. A term the API would reject as too short is not sent; the list stays as it is and a warning explains why.
    /// </summary>
    [RelayCommand]
    private Task SearchAsync()
    {
        if (PageQuery.IsSearchTooShort(Search))
        {
            Toasts.Show(Localization.Format("Search.TooShort", PageQuery.MinSearchLength), kind: ToastKind.Warning);
            return Task.CompletedTask;
        }

        _appliedSearch = Search;
        return LoadAsync(page: 1, includeTotal: true);
    }

    /// <summary>The search last submitted (Enter or the button). Loads use it, so half-typed text never filters the list.</summary>
    private string? _appliedSearch;

    protected abstract Task<ApiResult<PagedResult<TItem>>> FetchPageAsync(
        int page, int pageSize, string? search, bool includeTotal, CancellationToken cancellationToken);

    /// <summary>
    /// Loads the shown page again. Pass <paramref name="includeTotal"/> false after an edit (the count is unchanged);
    /// keep true after add/delete.
    /// </summary>
    protected Task ReloadAsync(bool includeTotal = true) => LoadAsync(CurrentPage, includeTotal);

    private async Task LoadAsync(int page, bool includeTotal)
    {
        var cancellation = BeginLoad();
        var cancellationToken = cancellation.Token;
        try
        {
            var result = await FetchPageAsync(Math.Max(1, page), PageSize, _appliedSearch, includeTotal, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // Nothing is left on this page (e.g. its last row was deleted): show the new last page.
            if (result.IsSuccess && result.Value!.Items.Count == 0 && page > 1)
            {
                var total = includeTotal && result.Value.TotalCount != PageQuery.TotalNotComputed
                    ? result.Value.TotalCount
                    : TotalCount;

                // Page flips skip the total; reuse the cached count instead of a middle GET.
                if (total <= 0)
                {
                    result = await FetchPageAsync(page, PageSize, _appliedSearch, includeTotal: true, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (result.IsSuccess)
                    {
                        total = result.Value!.TotalCount;
                    }
                }

                if (result.IsSuccess && total > 0)
                {
                    page = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
                    result = await FetchPageAsync(page, PageSize, _appliedSearch, includeTotal: false, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    includeTotal = false;
                }
            }

            if (result.IsSuccess)
            {
                Show(result.Value!, includeTotal);
            }
            else
            {
                ShowLoadFailure(result.Error);
            }

            MarkLoaded();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer load replaced this one and owns the page state now, or the user left the page.
        }
        catch (Exception ex)
        {
            ShowLoadFailure(ex);
        }
        finally
        {
            EndLoad(cancellation);
        }
    }

    private void Show(PagedResult<TItem> result, bool includeTotal)
    {
        LoadError = null;
        Items.ReplaceAll(result.Items);
        if (includeTotal && result.TotalCount != PageQuery.TotalNotComputed)
        {
            TotalCount = result.TotalCount;
        }

        var pageCount = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        if (PageQuery.IsTotalCapped(TotalCount) && result.Page >= pageCount && result.Items.Count >= PageSize)
        {
            // The count stopped at its cap, so there may be more rows after this full page: keep "next" available.
            pageCount = result.Page + 1;
        }

        ShowPaging(pageCount, result.Page);
    }
}
