using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Common;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;

namespace Mostawsef.Desktop.ViewModels.Shell;

/// <summary>
/// Base for list pages paged by page number (PageQuery / PagedResult): records sorted by name or another field, e.g. users.
/// Same bindings as <see cref="PagedListViewModel{TItem}"/>, which is for append-only, newest-first logs (cursor paging).
/// The portal shell activates the page when it is shown (first load) and deactivates it when the user leaves,
/// which cancels its API calls. Page flips skip the filtered total; search and refresh ask for it again.
/// Rows live in one <see cref="ListPageItems{T}"/> instance so the DataGrid is not torn down on every reload.
/// </summary>
public abstract partial class NumberedListViewModel<TItem> : ViewModelBase, ILoadablePage
{
    public const int DefaultPageSize = 20;

    private bool _suppressPageReload;
    private bool _loaded;
    private readonly PageLifetime _pageLifetime = new();
    private CancellationTokenSource? _loadCancellation;

    protected NumberedListViewModel(IToastService toasts, LocalizationService localization)
    {
        Toasts = toasts;
        Localization = localization;
    }

    protected IToastService Toasts { get; }

    protected LocalizationService Localization { get; }

    /// <summary>Bound once for the page lifetime; reloads replace contents in place.</summary>
    public ListPageItems<TItem> Items { get; } = new();

    [ObservableProperty]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    public partial int PageCount { get; private set; } = 1;

    [ObservableProperty]
    public partial int TotalCount { get; private set; }

    /// <summary>Number of the first shown row across all pages (41 on page 3 of 20 per page). Bind it to DataTable.FirstRowNumber.</summary>
    [ObservableProperty]
    public partial int FirstRowNumber { get; private set; } = 1;

    [ObservableProperty]
    public partial string? Search { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; private set; }

    /// <summary>Why the last load failed; null after a successful load.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError), nameof(LoadErrorTitleKey), nameof(LoadErrorDescriptionKey), nameof(IsEmpty))]
    public partial ApiError? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    public string? LoadErrorTitleKey => LoadError switch
    {
        null => null,
        ApiError.Unreachable => "Error.UnreachableTitle",
        _ => "Error.Title",
    };

    public string? LoadErrorDescriptionKey => LoadError switch
    {
        null => null,
        ApiError.Unreachable => "Error.UnreachableDescription",
        _ => "Error.Description",
    };

    public bool IsEmpty => !IsLoading && !HasLoadError && Items.Count == 0;

    public int PageSize { get; init; } = DefaultPageSize;

    public Task ActivateAsync()
    {
        _pageLifetime.EnsureActive();
        return _loaded ? Task.CompletedTask : ReloadAsync();
    }

    public void Deactivate()
    {
        if (_loadCancellation is not null)
        {
            _loaded = false;
        }

        _pageLifetime.Cancel();
        IsLoading = false;
    }

    /// <summary>The next activation loads the page again, e.g. when the user comes back from a sub-page that added a record.</summary>
    public void MarkStale() => _loaded = false;

    /// <summary>
    /// Puts a newly created row on page 1 without a server reload. Marks the page stale when a search is active
    /// or the user is not on page 1 (the new row may not belong on the screen).
    /// </summary>
    protected void PrependNewest(TItem item)
    {
        if (!_loaded || CurrentPage != 1 || !string.IsNullOrWhiteSpace(Search))
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
        if (!_loaded || CurrentPage != 1 || !string.IsNullOrWhiteSpace(Search))
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
    private Task RefreshAsync() => LoadAsync(CurrentPage, includeTotal: true);

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

        return LoadAsync(page: 1, includeTotal: true);
    }

    partial void OnCurrentPageChanged(int value)
    {
        if (!_suppressPageReload)
        {
            _ = LoadAsync(value, includeTotal: false);
        }
    }

    protected abstract Task<ApiResult<PagedResult<TItem>>> FetchPageAsync(
        int page, int pageSize, string? search, bool includeTotal, CancellationToken cancellationToken);

    /// <summary>
    /// Loads the shown page again. Pass <paramref name="includeTotal"/> false after an edit (the count is unchanged);
    /// keep true after add/delete.
    /// </summary>
    protected Task ReloadAsync(bool includeTotal = true) => LoadAsync(CurrentPage, includeTotal);

    /// <summary>Starting a load cancels the one still running, so only the newest page and search are shown.</summary>
    private async Task LoadAsync(int page, bool includeTotal)
    {
        _loadCancellation?.Cancel();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_pageLifetime.Token);
        _loadCancellation = cancellation;
        var cancellationToken = cancellation.Token;

        IsLoading = true;
        try
        {
            var result = await FetchPageAsync(Math.Max(1, page), PageSize, Search, includeTotal, cancellationToken);
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
                    result = await FetchPageAsync(page, PageSize, Search, includeTotal: true, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (result.IsSuccess)
                    {
                        total = result.Value!.TotalCount;
                    }
                }

                if (result.IsSuccess && total > 0)
                {
                    page = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
                    result = await FetchPageAsync(page, PageSize, Search, includeTotal: false, cancellationToken);
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
                Items.ReplaceAll([]);
                TotalCount = 0;
                LoadError = result.Error;
                OnPropertyChanged(nameof(IsEmpty));
            }

            _loaded = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer load replaced this one and owns the page state now, or the user left the page.
        }
        catch (Exception)
        {
            Items.ReplaceAll([]);
            TotalCount = 0;
            LoadError = ApiError.Server;
            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
            if (_loadCancellation == cancellation)
            {
                _loadCancellation = null;
                IsLoading = false;
            }

            cancellation.Dispose();
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

        // PageCount first: Pagination clamps CurrentPage to it and writes the clamped value back.
        _suppressPageReload = true;
        PageCount = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        CurrentPage = result.Page;
        _suppressPageReload = false;
        FirstRowNumber = (result.Page - 1) * PageSize + 1;
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>An API read tied to the page (e.g. details for a modal): null when the user left the page before it finished.</summary>
    protected async Task<ApiResult<T>?> ReadAsync<T>(Func<CancellationToken, Task<ApiResult<T>>> read)
    {
        var cancellationToken = _pageLifetime.Token;
        try
        {
            var result = await read(cancellationToken);
            return cancellationToken.IsCancellationRequested ? null : result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>Toast for a failed action: the API's error code when it has a text, otherwise a generic message.</summary>
    protected void ShowError(IApiResult result)
    {
        var code = result.Problem?.Code;
        var codeKey = code is null ? null : "ErrorCode." + code;
        Toasts.Error(Localization.Get(
            result.Error == ApiError.Unreachable ? "Login.ServerUnreachable"
            : codeKey is not null && Localization.Get(codeKey) != codeKey ? codeKey
            : "Error.RequestFailed"));
    }

}
