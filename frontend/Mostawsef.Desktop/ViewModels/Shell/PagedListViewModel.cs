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
/// List pages backed by a keyset-paged API endpoint (append-only, newest-first logs). The shared state and
/// lifecycle live in <see cref="ListPageViewModelBase{TItem}"/>. Subclasses implement FetchPageAsync (and optionally
/// LoadExtrasAsync, e.g. stat cards). The total is counted only when the filter changes or on Refresh, and the
/// extras only on the first load and Refresh. Row numbers count from the newest (21 on page 2 of 20 per page).
/// </summary>
public abstract partial class PagedListViewModel<TItem> : ListPageViewModelBase<TItem>
{
    // The page on screen: how it was requested, its number and the cursors to its neighbours.
    private CursorQuery? _shownQuery;
    private int _shownPage = 1;
    private string? _nextCursor;
    private string? _previousCursor;

    protected PagedListViewModel(IToastService toasts, LocalizationService localization, ErrorReporter errors)
        : base(toasts, localization, errors)
    {
    }

    protected override Task LoadOnActivateAsync() => ReloadAsync();

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
    protected override void OnPageSelected(int page)
    {
        var search = _shownQuery?.Search;
        var (query, target) =
            page <= 1 ? (FirstPage(search), 1)
            : page >= PageCount && page > _shownPage + 1 ? (LastPage(search), PageCount)
            : page > _shownPage && _nextCursor is not null ? (FirstPage(search) with { After = _nextCursor }, _shownPage + 1)
            : page < _shownPage && _previousCursor is not null ? (FirstPage(search) with { Before = _previousCursor }, _shownPage - 1)
            : page > _shownPage ? (LastPage(search), PageCount)
            : (FirstPage(search), 1);

        _ = LoadAsync(query with { IncludeTotal = false }, target, withExtras: false);
    }

    protected abstract Task<ApiResult<CursorPage<TItem>>> FetchPageAsync(CursorQuery query, CancellationToken cancellationToken);

    /// <summary>Extra data shown with the list, e.g. stat cards. Loaded on the first load, Refresh and ReloadAsync only.</summary>
    protected virtual Task LoadExtrasAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Unfiltered table total from extras (e.g. stats). When set and the list has no search, the list skips its own COUNT.
    /// </summary>
    protected virtual int? ExtrasTotalCount => null;

    /// <summary>Loads the shown page again with extras; counts only when a search filter is active (stats cover the unfiltered total).</summary>
    protected Task ReloadAsync()
    {
        var query = _shownQuery ?? FirstPage(Search);
        var includeTotal = !string.IsNullOrWhiteSpace(query.Search);
        return LoadAsync(query with { IncludeTotal = includeTotal }, _shownPage, withExtras: true);
    }

    private CursorQuery FirstPage(string? search) => new(PageSize, search);

    /// <summary>The oldest rows, as many as the last page holds when counting from the newest, so pages line up both ways.</summary>
    private CursorQuery LastPage(string? search)
    {
        var onLastPage = TotalCount % PageSize;
        return new CursorQuery(onLastPage == 0 ? PageSize : onLastPage, search, Last: true);
    }

    private int PageCountFor(int total) => Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));

    /// <summary>
    /// When extras cover the unfiltered total and there is no search, stats and the page load in parallel
    /// so the list skips COUNT without waiting for stats first.
    /// </summary>
    private async Task LoadAsync(CursorQuery query, int page, bool withExtras)
    {
        var cancellation = BeginLoad();
        var cancellationToken = cancellation.Token;
        var useExtrasTotal = withExtras && string.IsNullOrWhiteSpace(query.Search);
        Task? extras = withExtras ? LoadExtrasAsync(cancellationToken) : null;
        try
        {
            if (useExtrasTotal)
            {
                query = query with { IncludeTotal = false };
            }

            var result = await FetchPageAsync(query, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (extras is not null)
            {
                await extras;
                extras = null;
                cancellationToken.ThrowIfCancellationRequested();
                if (useExtrasTotal && ExtrasTotalCount is { } extrasTotal)
                {
                    TotalCount = extrasTotal;
                }
            }

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
            if (extras is not null)
            {
                try
                {
                    await extras;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    // Observe a racing extras fault so it is not abandoned when the page fetch failed first.
                    Errors.Report(ex, context: $"Loading extras for {GetType().Name}");
                }
            }

            EndLoad(cancellation);
        }
    }

    private void Show(CursorQuery query, int page, CursorPage<TItem> result)
    {
        _shownQuery = query;
        _nextCursor = result.NextCursor;
        _previousCursor = result.PreviousCursor;
        LoadError = null;
        Items.ReplaceAll(result.Items);
        if (result.TotalCount is { } total)
        {
            TotalCount = total;
        }

        // The cursors know where the ends are; the total can be older than the rows, since logs keep arriving.
        _shownPage = result.PreviousCursor is null ? 1 : page;
        var pageCount = result.NextCursor is null ? _shownPage : Math.Max(PageCountFor(TotalCount), _shownPage + 1);
        ShowPaging(pageCount, _shownPage);
    }
}
