using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Mostawsef.Contracts.Common;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;

namespace Mostawsef.Desktop.ViewModels.Shell;

/// <summary>
/// What every list page shares, whatever its paging strategy (<see cref="NumberedListViewModel{TItem}"/> or
/// <see cref="PagedListViewModel{TItem}"/>): the bindable state (rows, paging, search, loading, error), activation,
/// and load cancellation. The portal shell activates the page when it is shown (first load) and deactivates it when
/// the user leaves, which cancels its API calls. Rows live in one <see cref="ListPageItems{T}"/> instance so the
/// DataGrid is not torn down on every reload.
/// </summary>
public abstract partial class ListPageViewModelBase<TItem> : ViewModelBase, ILoadablePage
{
    public const int DefaultPageSize = 20;

    private readonly PageLifetime _pageLifetime = new();
    private CancellationTokenSource? _loadCancellation;
    private bool _suppressPageReload;
    private bool _loaded;

    protected ListPageViewModelBase(IToastService toasts, LocalizationService localization, ErrorReporter errors)
    {
        Toasts = toasts;
        Localization = localization;
        Errors = errors;
    }

    protected IToastService Toasts { get; }

    protected LocalizationService Localization { get; }

    protected ErrorReporter Errors { get; }

    /// <summary>Bound once for the page lifetime; reloads replace contents in place.</summary>
    public ListPageItems<TItem> Items { get; } = new();

    public int PageSize { get; init; } = DefaultPageSize;

    [ObservableProperty]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    public partial int PageCount { get; protected set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTotal))]
    public partial int TotalCount { get; protected set; }

    /// <summary>
    /// The total for "{0} records" texts: the number, or "10,000+" when the API stopped counting
    /// (<see cref="PageQuery.MaxCountedTotal"/>). Formatted with the UI language when shown.
    /// </summary>
    public object DisplayTotal => PageQuery.IsTotalCapped(TotalCount) ? new AtLeastCount(TotalCount) : TotalCount;

    /// <summary>Number of the first shown row across all pages (41 on page 3 of 20 per page). Bind it to DataTable.FirstRowNumber.</summary>
    [ObservableProperty]
    public partial int FirstRowNumber { get; protected set; } = 1;

    [ObservableProperty]
    public partial string? Search { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; private set; }

    /// <summary>Why the last load failed; null after a successful load.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError), nameof(LoadErrorTitleKey), nameof(LoadErrorDescriptionKey), nameof(IsEmpty))]
    public partial ApiError? LoadError { get; protected set; }

    public bool HasLoadError => LoadError is not null;

    public string? LoadErrorTitleKey => ApiErrorKeys.Title(LoadError);

    public string? LoadErrorDescriptionKey => ApiErrorKeys.Description(LoadError);

    public bool IsEmpty => !IsLoading && !HasLoadError && Items.Count == 0;

    /// <summary>True once a load finished (successfully or not) since the page was last activated.</summary>
    protected bool IsLoaded => _loaded;

    public Task ActivateAsync()
    {
        _pageLifetime.EnsureActive();
        return _loaded ? Task.CompletedTask : LoadOnActivateAsync();
    }

    public virtual void Deactivate()
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

    /// <summary>The first load of an activation (the subclass's full reload).</summary>
    protected abstract Task LoadOnActivateAsync();

    /// <summary>The user moved to <paramref name="page"/> (not raised while a load writes the shown page back).</summary>
    protected abstract void OnPageSelected(int page);

    partial void OnCurrentPageChanged(int value)
    {
        if (!_suppressPageReload)
        {
            OnPageSelected(value);
        }
    }

    /// <summary>Starting a load cancels the one still running, so only the newest page and search are shown.</summary>
    protected CancellationTokenSource BeginLoad()
    {
        _loadCancellation?.Cancel();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_pageLifetime.Token);
        _loadCancellation = cancellation;
        IsLoading = true;
        return cancellation;
    }

    /// <summary>Marks the load done unless a newer one has replaced it, and releases its token source.</summary>
    protected void EndLoad(CancellationTokenSource cancellation)
    {
        if (_loadCancellation == cancellation)
        {
            _loadCancellation = null;
            IsLoading = false;
        }

        cancellation.Dispose();
    }

    protected void MarkLoaded() => _loaded = true;

    /// <summary>A failed load: no rows, no total, and the reason.</summary>
    protected void ShowLoadFailure(ApiError error)
    {
        Items.ReplaceAll([]);
        TotalCount = 0;
        LoadError = error;
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>Reports an unexpected exception from a load and shows the failure state.</summary>
    protected void ShowLoadFailure(Exception exception)
    {
        Errors.Report(exception, context: $"Loading {GetType().Name}");
        ShowLoadFailure(ApiError.Server);
    }

    /// <summary>Writes the shown page's numbers back to the bindable state without triggering a page load.</summary>
    protected void ShowPaging(int pageCount, int shownPage)
    {
        // PageCount first: Pagination clamps CurrentPage to it and writes the clamped value back.
        _suppressPageReload = true;
        PageCount = pageCount;
        CurrentPage = shownPage;
        _suppressPageReload = false;
        FirstRowNumber = (shownPage - 1) * PageSize + 1;
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

    protected void ShowError(ApiError error) =>
        Toasts.Error(Localization.Get(error == ApiError.Unreachable ? "Login.ServerUnreachable" : "Error.RequestFailed"));
}

/// <summary>A count shown as "at least": 10000 formats as "10,000+" (with the culture of the text it is formatted into).</summary>
public readonly record struct AtLeastCount(int Value) : IFormattable
{
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        Value.ToString("N0", formatProvider) + "+";

    public override string ToString() => ToString(null, System.Globalization.CultureInfo.CurrentCulture);
}
