using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Orders;
using Mostawsef.Contracts.Patients;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Admin;

/// <summary>
/// Admin portal sub-page: one patient's details and their orders as cards (newest first, up to
/// <see cref="OrdersShown"/>); a card opens that order's details. Call <see cref="Show"/> before it opens.
/// </summary>
public partial class PatientDetailsViewModel(OrdersApi orders, NavigationService navigation)
    : ViewModelBase, ILoadablePage, IReusablePage, IBreadcrumbPage
{
    /// <summary>The most orders shown (the API's largest page).</summary>
    public const int OrdersShown = PageQuery.MaxPageSize;

    private CancellationTokenSource? _loadCancellation;

    /// <summary>Tells the patients list whether this patient has an unpaid order (so its row tint stays right).</summary>
    private Action<Guid, bool>? _unpaidKnown;

    /// <summary>One Order details page for this patient page; reset on each open so PageHost can reuse its view.</summary>
    private OrderDetailsViewModel? _orderDetails;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPatient), nameof(BreadcrumbArg))]
    public partial PatientDto? Patient { get; private set; }

    public bool HasPatient => Patient is not null;

    public ObservableCollection<OrderListItemDto> Orders { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError), nameof(LoadErrorTitleKey), nameof(LoadErrorDescriptionKey), nameof(IsEmpty))]
    public partial ApiError? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    /// <summary>The patient has no orders (and the list is not loading or failed).</summary>
    public bool IsEmpty => !IsLoading && !HasLoadError && Orders.Count == 0;

    public string? LoadErrorTitleKey => ApiErrorKeys.Title(LoadError);

    public string? LoadErrorDescriptionKey => ApiErrorKeys.Description(LoadError);

    /// <summary>The patient has more orders than are shown: only the newest <see cref="OrdersShown"/> are listed.</summary>
    [ObservableProperty]
    public partial bool IsTruncated { get; private set; }

    /// <summary>The breadcrumb shows the patient's name after the menu page.</summary>
    public string BreadcrumbKey => "Breadcrumb.Name";

    public string? BreadcrumbArg => Patient?.FullName;

    /// <summary>Points the page at <paramref name="patient"/> and clears the previous patient's orders (the page is reused).</summary>
    public void Show(PatientDto patient, Action<Guid, bool>? unpaidKnown = null)
    {
        _unpaidKnown = unpaidKnown;
        IsTruncated = false;
        _loadCancellation?.Cancel();
        _loadCancellation = null;
        Orders.Clear();
        LoadError = null;
        IsLoading = false;
        Patient = patient;
        OnPropertyChanged(nameof(IsEmpty));
    }

    public Task ActivateAsync() => Patient is null ? Task.CompletedTask : LoadAsync();

    public void Deactivate()
    {
        _loadCancellation?.Cancel();
        IsLoading = false;
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private Task OpenOrderAsync(OrderListItemDto? order)
    {
        if (order is null)
        {
            return Task.CompletedTask;
        }

        return navigation.Reuse(ref _orderDetails) is { } details
            ? navigation.OpenAsync(details, page =>
            {
                page.BeginReuse();
                page.OrderId = order.Id;
            })
            : Task.CompletedTask;
    }

    private async Task LoadAsync()
    {
        if (Patient is not { } patient)
        {
            return;
        }

        _loadCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;

        IsLoading = true;
        try
        {
            var result = await orders.GetOrdersAsync(
                1, OrdersShown, search: null, includeTotal: false, status: null, patientId: patient.Id, cancellationToken: cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();

            Orders.Clear();
            if (result.IsSuccess)
            {
                foreach (var order in result.Value!.Items)
                {
                    Orders.Add(order);
                }
            }

            LoadError = result.IsSuccess ? null : result.Error;
            if (result.IsSuccess)
            {
                IsTruncated = Orders.Count >= OrdersShown;
                var unpaid = Orders.Any(o => o.Status is OrderStatuses.Pending or OrderStatuses.PartiallyPaid);

                // With more orders than shown, "none unpaid" among the newest says nothing about the rest.
                if (unpaid || !IsTruncated)
                {
                    _unpaidKnown?.Invoke(patient.Id, unpaid);
                }
            }
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

            OnPropertyChanged(nameof(IsEmpty));
        }
    }
}
