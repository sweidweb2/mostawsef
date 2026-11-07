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
/// Base for list pages backed by a paged API endpoint: items, paging, search, loading/empty/error state.
/// Subclasses implement FetchPageAsync (and optionally LoadExtrasAsync, e.g. stat cards). Paging is keyset based:
/// the total is counted only when the filter changes or on Refresh, and the extras only on the first load and Refresh.
/// The portal shell activates the page when it is shown (first load) and deactivates it when the user leaves,
/// which cancels its API calls.
/// </summary>
public abstract partial class PagedListViewModel<TItem> : ViewModelBase, ILoadablePage
{
    public const int DefaultPageSize = 20;

    private bool _suppressPageReload;
    private bool _loaded;
    private CancellationTokenSource _pageCancellation = new();
    private CancellationTokenSource? _loadCancellation;

    // The page on screen: how it was requested, its number and the cursors to its neighbours.
    private CursorQuery? _shownQuery;
    private int _shownPage = 1;
    private string? _nextCursor;
    private string? _previousCursor;

    protected PagedListViewModel(IToastService toasts, LocalizationService localization)
    {
        Toasts = toasts;
        Localization = localization;
    }

    protected IToastService Toasts { get; }

    protected LocalizationService Localization { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial IReadOnlyList<TItem> Items { get; private set; } = [];

    [ObservableProperty]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    public partial int PageCount { get; private set; } = 1;

    [ObservableProperty]
    public partial int TotalCount { get; private set; }

    /// <summary>Number of the first shown row, counted from the newest (21 on page 2 of 20 per page). Bind it to DataTable.FirstRowNumber.</summary>
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
        if (_pageCancellation.IsCancellationRequested)
        {
            _pageCancellation = new CancellationTokenSource();
        }

        return _loaded ? Task.CompletedTask : ReloadAsync();
    }

    public void Deactivate()
    {
        if (_loadCancellation is not null)
        {
            _loaded = false;
        }

        _pageCancellation.Cancel();
    }

    [RelayCommand]
    private Task RefreshAsync() => ReloadAsync();

    /// <summary>
    /// A new filter: count its matches again, but the extras (stats of the whole table) stay as they are.
    /// A term the API would reject as too short is not sent; the list stays as it is and a warning explains why.
    /// </summary>
    [RelayCommand]
    private Task SearchAsync()
    {
        if (CursorQuery.IsSearchTooShort(Search))
        {
            Toasts.Show(Localization.Format("Search.TooShort", CursorQuery.MinSearchLength), kind: ToastKind.Warning);
            return Task.CompletedTask;
        }

        return LoadAsync(FirstPage(Search), page: 1, withExtras: false);
    }

    /// <summary>
    /// Pagination moves one page at a time (or to the first/last page), so each move is one keyset request from the
    /// shown page's cursors. Moves skip the total and the extras: neither depends on which page is shown.
    /// </summary>
    partial void OnCurrentPageChanged(int value)
    {
        if (_suppressPageReload)
        {
            return;
        }

        var search = _shownQuery?.Search;
        var (query, page) =
            value <= 1 ? (FirstPage(search), 1)
            : value >= PageCount && value > _shownPage + 1 ? (LastPage(search), PageCount)
            : value > _shownPage && _nextCursor is not null ? (FirstPage(search) with { After = _nextCursor }, _shownPage + 1)
            : value < _shownPage && _previousCursor is not null ? (FirstPage(search) with { Before = _previousCursor }, _shownPage - 1)
            : value > _shownPage ? (LastPage(search), PageCount)
            : (FirstPage(search), 1);

        _ = LoadAsync(query with { IncludeTotal = false }, page, withExtras: false);
    }

    protected abstract Task<ApiResult<CursorPage<TItem>>> FetchPageAsync(CursorQuery query, CancellationToken cancellationToken);

    /// <summary>Extra data shown with the list, e.g. stat cards. Loaded on the first load, Refresh and ReloadAsync only.</summary>
    protected virtual Task LoadExtrasAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Loads the shown page again with a fresh total and extras, e.g. after one of its rows was deleted.</summary>
    protected Task ReloadAsync() =>
        LoadAsync((_shownQuery ?? FirstPage(Search)) with { IncludeTotal = true }, _shownPage, withExtras: true);

    private CursorQuery FirstPage(string? search) => new(PageSize, search);

    /// <summary>The oldest rows, as many as the last page holds when counting from the newest, so pages line up both ways.</summary>
    private CursorQuery LastPage(string? search)
    {
        var onLastPage = TotalCount % PageSize;
        return new CursorQuery(onLastPage == 0 ? PageSize : onLastPage, search, Last: true);
    }

    private int PageCountFor(int total) => Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));

    /// <summary>
    /// Starting a load cancels the one still running, so only the newest page and search are shown.
    /// Leaving the page cancels it too; the next activation loads again.
    /// </summary>
    private async Task LoadAsync(CursorQuery query, int page, bool withExtras)
    {
        _loadCancellation?.Cancel();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_pageCancellation.Token);
        _loadCancellation = cancellation;
        var cancellationToken = cancellation.Token;

        IsLoading = true;
        try
        {
            var extras = withExtras ? LoadExtrasAsync(cancellationToken) : Task.CompletedTask;
            var result = await FetchPageAsync(query, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // Nothing is left on this page (e.g. its last row was deleted): show the new last page.
            if (result.IsSuccess && result.Value!.Items.Count == 0 && page > 1)
            {
                if (result.Value.TotalCount is { } total)
                {
                    TotalCount = total;
                }

                page = PageCountFor(TotalCount);
                query = LastPage(query.Search) with { IncludeTotal = false };
                result = await FetchPageAsync(query, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (result.IsSuccess)
            {
                Show(query, page, result.Value!);
            }
            else
            {
                Items = [];
                TotalCount = 0;
                LoadError = result.Error;
            }

            await extras;
            _loaded = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer load replaced this one and owns the page state now, or the user left the page.
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

    private void Show(CursorQuery query, int page, CursorPage<TItem> result)
    {
        _shownQuery = query;
        _nextCursor = result.NextCursor;
        _previousCursor = result.PreviousCursor;
        LoadError = null;
        Items = result.Items;
        if (result.TotalCount is { } total)
        {
            TotalCount = total;
        }

        // The cursors know where the ends are; the total can be older than the rows, since logs keep arriving.
        _shownPage = result.PreviousCursor is null ? 1 : page;
        var pageCount = result.NextCursor is null ? _shownPage : Math.Max(PageCountFor(TotalCount), _shownPage + 1);

        // PageCount first: Pagination clamps CurrentPage to it and writes the clamped value back.
        _suppressPageReload = true;
        PageCount = pageCount;
        CurrentPage = _shownPage;
        _suppressPageReload = false;
        FirstRowNumber = (_shownPage - 1) * PageSize + 1;
    }

    /// <summary>An API read tied to the page (e.g. details for a modal): null when the user left the page before it finished.</summary>
    protected async Task<ApiResult<T>?> ReadAsync<T>(Func<CancellationToken, Task<ApiResult<T>>> read)
    {
        var cancellationToken = _pageCancellation.Token;
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

    protected void ShowError(ApiError error) =>
        Toasts.Error(Localization.Get(error == ApiError.Unreachable ? "Login.ServerUnreachable" : "Error.RequestFailed"));
}
