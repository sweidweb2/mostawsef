using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Orders;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Admin;

/// <summary>Admin portal sub-page: one order's main details and tests (read-only). Set <see cref="OrderId"/> before it opens; optionally <see cref="Seed"/> to skip the first GET.</summary>
public partial class OrderDetailsViewModel(OrdersApi orders, NavigationService navigation) : ViewModelBase, ILoadablePage, IReusablePage
{
    private CancellationTokenSource? _loadCancellation;

    public Guid OrderId { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOrder), nameof(HasAddon))]
    public partial OrderDetailsDto? Order { get; private set; }

    public bool HasOrder => Order is not null;

    /// <summary>True when the order has an amount added on top of the tests' catalog prices.</summary>
    public bool HasAddon => Order is { Addon: > 0 };

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError), nameof(LoadErrorTitleKey), nameof(LoadErrorDescriptionKey))]
    public partial ApiError? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    public string? LoadErrorTitleKey => LoadError switch
    {
        null => null,
        ApiError.Unreachable => "Error.UnreachableTitle",
        ApiError.NotFound => "OrderDetails.NotFound",
        _ => "Error.Title",
    };

    public string? LoadErrorDescriptionKey => LoadError switch
    {
        null => null,
        ApiError.Unreachable => "Error.UnreachableDescription",
        ApiError.NotFound => null,
        _ => "Error.Description",
    };

    /// <summary>Clears prior order state so Orders can reuse one instance (and its PageHost view) per portal visit.</summary>
    public void BeginReuse()
    {
        _loadCancellation?.Cancel();
        _loadCancellation = null;
        OrderId = default;
        Order = null;
        LoadError = null;
        IsLoading = false;
    }

    /// <summary>Shows <paramref name="order"/> immediately (e.g. after create) so Activate skips the GET.</summary>
    public void Seed(OrderDetailsDto order)
    {
        _loadCancellation?.Cancel();
        _loadCancellation = null;
        OrderId = order.Id;
        Order = order;
        LoadError = null;
        IsLoading = false;
    }

    public Task ActivateAsync() => Order is null ? LoadAsync() : Task.CompletedTask;

    public void Deactivate()
    {
        _loadCancellation?.Cancel();
        IsLoading = false;
        Order = null;
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private Task<bool> BackAsync() => navigation.BackAsync();

    private async Task LoadAsync()
    {
        _loadCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;

        IsLoading = true;
        try
        {
            var result = await orders.GetOrderAsync(OrderId, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            Order = result.Value;
            LoadError = result.IsSuccess ? null : result.Error;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // The user left the page; the next activation loads again.
        }
        finally
        {
            if (_loadCancellation == cancellation)
            {
                _loadCancellation = null;
                IsLoading = false;
            }
        }
    }
}
