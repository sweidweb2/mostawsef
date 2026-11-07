using System;
using System.Collections.Generic;
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
/// Subclasses implement FetchPageAsync (and optionally LoadExtrasAsync for stats).
/// </summary>
public abstract partial class PagedListViewModel<TItem> : ViewModelBase, ILoadablePage
{
    public const int DefaultPageSize = 20;

    private readonly IToastService _toasts;
    private bool _suppressPageReload;

    protected PagedListViewModel(IToastService toasts)
    {
        _toasts = toasts;
        LocalizationService.Instance.LanguageChanged += (_, _) => RefreshItemsDisplay();
    }

    [ObservableProperty]
    public partial IReadOnlyList<TItem> Items { get; private set; } = [];

    [ObservableProperty]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    public partial int PageCount { get; private set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalLabel))]
    public partial int TotalCount { get; private set; }

    /// <summary>"N records", translated.</summary>
    public string TotalLabel => LocalizationService.Instance.Format("Logs.Records", TotalCount);

    [ObservableProperty]
    public partial string? Search { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; private set; }

    public bool IsEmpty => !IsLoading && Items.Count == 0;

    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>Loads the first page (and extras). Called when the page opens.</summary>
    public Task InitializeAsync() => LoadAsync(resetPage: true);

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync(resetPage: false);

    [RelayCommand]
    private Task SearchAsync() => LoadAsync(resetPage: true);

    partial void OnCurrentPageChanged(int value)
    {
        if (!_suppressPageReload)
        {
            _ = LoadAsync(resetPage: false);
        }
    }

    protected abstract Task<ApiResult<PagedResult<TItem>>> FetchPageAsync(int page, int pageSize, string? search);

    /// <summary>Extra data shown with the list, e.g. stat cards.</summary>
    protected virtual Task LoadExtrasAsync() => Task.CompletedTask;

    protected async Task LoadAsync(bool resetPage)
    {
        if (resetPage && CurrentPage != 1)
        {
            _suppressPageReload = true;
            CurrentPage = 1;
            _suppressPageReload = false;
        }

        IsLoading = true;
        try
        {
            var extras = LoadExtrasAsync();
            var result = await FetchPageAsync(CurrentPage, PageSize, Search);

            if (result.IsSuccess)
            {
                var page = result.Value!;
                Items = page.Items;
                TotalCount = page.TotalCount;
                PageCount = Math.Max(1, page.TotalPages);

                // Deleting the last item of the last page: step back one page.
                if (page.Items.Count == 0 && CurrentPage > 1 && page.TotalCount > 0)
                {
                    CurrentPage = PageCount;
                    return;
                }
            }
            else
            {
                ShowError(result.Error);
            }

            await extras;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    protected void ShowError(ApiError error) =>
        _toasts.Error(LocalizationService.Instance.Get(error == ApiError.Unreachable ? "Login.ServerUnreachable" : "Error.RequestFailed"));

    protected IToastService Toasts => _toasts;

    /// <summary>Re-applies converters (e.g. translated labels) after a language change.</summary>
    private void RefreshItemsDisplay()
    {
        OnPropertyChanged(nameof(TotalLabel));
        var items = Items;
        Items = [];
        Items = items;
    }
}
