using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Orders;
using Mostawsef.Contracts.Patients;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Admin;

/// <summary>
/// Admin portal sub-page: one patient's details and their orders as cards (newest first, up to
/// <see cref="OrdersShown"/>); a card opens that order's details. Call <see cref="Show"/> before it opens.
/// </summary>
public partial class PatientDetailsViewModel(OrdersApi orders, PatientsApi patients, NavigationService navigation, ErrorReporter errors)
    : ViewModelBase, ILoadablePage, IReusablePage, IBreadcrumbPage
{
    /// <summary>The most orders shown (the API's largest page).</summary>
    public const int OrdersShown = PageQuery.MaxPageSize;

    private readonly LatestLoad _load = new();

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
    public void Show(PatientDto patient)
    {
        IsTruncated = false;
        _load.Cancel();
        Orders.Clear();
        LoadError = null;
        IsLoading = false;
        Patient = patient;
        OnPropertyChanged(nameof(IsEmpty));
    }

    public Task ActivateAsync() => Patient is null ? Task.CompletedTask : LoadAsync();

    public void Deactivate()
    {
        _load.Cancel();
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

    /// <summary>The full patient record, or null when the request failed or was cancelled (the list row's data stays); never throws.</summary>
    private async Task<ApiResult<PatientDto>?> TryGetPatientAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return await patients.GetPatientAsync(id, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errors.Report(ex, context: "Loading the full patient record");
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private async Task LoadAsync()
    {
        if (Patient is not { } patient)
        {
            return;
        }

        using var cancellation = _load.Begin();

        IsLoading = true;
        try
        {
            var full = TryGetPatientAsync(patient.Id, cancellation.Token);
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
            IsTruncated = result.IsSuccess && Orders.Count >= OrdersShown;

            // The list row has no notes; the full record fills them in. A failure keeps what the list had.
            if (await full is { IsSuccess: true, Value: { } loaded } && Patient?.Id == patient.Id)
            {
                Patient = loaded;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // The user left the page; the next activation loads again.
        }
        catch (Exception ex)
        {
            errors.Report(ex, context: $"Loading {nameof(PatientDetailsViewModel)}");
            Orders.Clear();
            LoadError = ApiError.Server;
        }
        finally
        {
            if (_load.Finish(cancellation))
            {
                IsLoading = false;
            }

            OnPropertyChanged(nameof(IsEmpty));
        }
    }
}
