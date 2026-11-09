using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Contracts.Orders;
using Mostawsef.Contracts.Patients;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Admin;

/// <summary>
/// Admin portal sub-page: a new order in three columns. Column 1 picks an existing patient (server-backed autocomplete)
/// or fills a new one, plus an optional first payment; column 2 holds the selected tests and the live total (with an
/// optional our-price override); column 3 is the active lab test catalog. Saving sends one request; the API saves it
/// all or nothing.
/// </summary>
public partial class NewOrderViewModel : FormViewModel, ILoadablePage
{
    public const int PatientTab = 0;
    public const int TestsTab = 1;
    public const int CatalogTab = 2;

    private readonly OrdersApi _orders;
    private readonly PatientsApi _patients;
    private readonly LabTestsApi _tests;
    private readonly NavigationService _navigation;
    private readonly IToastService _toasts;

    private CancellationTokenSource _pageCancellation = new();
    private CancellationTokenSource? _catalogSearchDelay;
    private CancellationTokenSource? _catalogLoadCancellation;
    private CancellationTokenSource? _patientSearchDelay;
    private CancellationTokenSource? _patientLoadCancellation;
    private bool _catalogLoaded;
    private string? _loadedCatalogSearch;
    private string? _loadedPatientSearch;
    private OrderDetailsDto? _created;

    /// <summary>Called after a successful create so the orders list reloads when the user returns.</summary>
    public Action? OnOrderCreated { get; set; }

    public static readonly TimeSpan CatalogSearchDelay = TimeSpan.FromMilliseconds(250);

    public static readonly TimeSpan PatientSearchDelay = CatalogSearchDelay;

    public NewOrderViewModel(
        OrdersApi orders,
        PatientsApi patients,
        LabTestsApi tests,
        NavigationService navigation,
        IDialogService dialogs,
        IToastService toasts,
        LocalizationService localization)
        : base(dialogs, localization)
    {
        _orders = orders;
        _patients = patients;
        _tests = tests;
        _navigation = navigation;
        _toasts = toasts;

        PatientForm = new PatientFormViewModel(patients, dialogs, localization);
        PatientForm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not (nameof(IsDirty) or nameof(IsBusy) or nameof(FormErrorKey) or nameof(HasFormError)))
            {
                InputChanged(nameof(PatientForm));
            }
        };
        SelectedTests.CollectionChanged += (_, _) => InputChanged(nameof(SelectedTests));
        Closed += OnClosed;

        MarkClean();
    }

    // ---- Patient (column 1) ----

    /// <summary>The new patient's fields, the same form as on the patients page.</summary>
    public PatientFormViewModel PatientForm { get; }

    /// <summary>False: pick an existing patient; true: fill in a new one.</summary>
    [ObservableProperty]
    public partial bool IsNewPatient { get; set; }

    /// <summary>Patients matching the current search (one API page). The autocomplete shows this list as-is.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoPatientsFound))]
    public partial IReadOnlyList<PatientDto> Patients { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoPatientsFound))]
    public partial bool IsLoadingPatients { get; private set; }

    /// <summary>Text in the patient autocomplete; debounced search against the API.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoPatientsFound))]
    public partial string? PatientSearch { get; set; }

    /// <summary>Bound to the autocomplete selection; picking one fills <see cref="SelectedPatient"/>.</summary>
    [ObservableProperty]
    public partial PatientDto? PatientPick { get; set; }

    public bool NoPatientsFound =>
        !IsLoadingPatients
        && SelectedPatient is null
        && !string.IsNullOrWhiteSpace(PatientSearch)
        && !PageQuery.IsSearchTooShort(PatientSearch)
        && _loadedPatientSearch == PatientSearch
        && Patients.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedPatient), nameof(NoPatientsFound))]
    public partial PatientDto? SelectedPatient { get; private set; }

    public bool HasSelectedPatient => SelectedPatient is not null;

    /// <summary>An existing patient is picked, or the new patient's required fields are filled in.</summary>
    public bool HasPatient => IsNewPatient ? PatientForm.HasRequiredFields : SelectedPatient is not null;

    // ---- First payment (column 1) ----

    public IReadOnlyList<string> PaymentMethodOptions => PaymentMethods.All;

    [ObservableProperty]
    public partial decimal? PaymentAmount { get; set; }

    [ObservableProperty]
    public partial string PaymentMethod { get; set; } = PaymentMethods.Cash;

    [ObservableProperty]
    public partial string PaymentNotes { get; set; } = string.Empty;

    public bool HasPayment => PaymentAmount is > 0;

    // ---- Selected tests (column 2) ----

    public ObservableCollection<OrderItemDraft> SelectedTests { get; } = [];

    public bool HasSelectedTests => SelectedTests.Count > 0;

    /// <summary>Highest our-price addon the input accepts, in currency units.</summary>
    public decimal MaxOurPrice { get; } = Money.ToMajor(OrderRules.MaxOurPrice);

    /// <summary>Sum of the selected tests' catalog prices, in minor units.</summary>
    public long CatalogTotal => SelectedTests.Sum(t => t.Test.Price);

    /// <summary>Optional amount added on top of the catalog total.</summary>
    [ObservableProperty]
    public partial decimal? OurPrice { get; set; }

    /// <summary>Catalog total plus our-price addon — what the patient can be charged, in minor units.</summary>
    public long Total => CatalogTotal + (OurPrice is { } price ? Money.ToMinor(price) : 0);

    /// <summary>Highest first payment allowed right now (the live total), in currency units.</summary>
    public decimal MaxPayableAmount => Money.ToMajor(Math.Min(Total, OrderRules.MaxPaymentAmount));

    public long Remaining => Total - (PaymentAmount is { } amount ? Money.ToMinor(amount) : 0);

    // ---- Catalog (column 3) ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCatalogEmpty))]
    public partial IReadOnlyList<CatalogTestItem> Catalog { get; private set; } = [];

    [ObservableProperty]
    public partial string? CatalogSearch { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCatalogEmpty))]
    public partial bool IsCatalogLoading { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCatalogError), nameof(CatalogErrorTitleKey), nameof(CatalogErrorDescriptionKey), nameof(IsCatalogEmpty))]
    public partial ApiError? CatalogLoadError { get; private set; }

    public bool HasCatalogError => CatalogLoadError is not null;

    public string? CatalogErrorTitleKey => CatalogLoadError switch
    {
        null => null,
        ApiError.Unreachable => "Error.UnreachableTitle",
        _ => "Error.Title",
    };

    public string? CatalogErrorDescriptionKey => CatalogLoadError switch
    {
        null => null,
        ApiError.Unreachable => "Error.UnreachableDescription",
        _ => "Error.Description",
    };

    /// <summary>Nothing to show: no active tests match the current search.</summary>
    public bool IsCatalogEmpty => !IsCatalogLoading && !HasCatalogError && Catalog.Count == 0;

    /// <summary>The visible column below the Compact breakpoint (PatientTab, TestsTab, CatalogTab).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPatientTab), nameof(IsTestsTab), nameof(IsCatalogTab))]
    public partial int SelectedTab { get; set; }

    public bool IsPatientTab => SelectedTab == PatientTab;

    public bool IsTestsTab => SelectedTab == TestsTab;

    public bool IsCatalogTab => SelectedTab == CatalogTab;

    public Task ActivateAsync()
    {
        EnsureLifetime();
        if (_pageCancellation.IsCancellationRequested)
        {
            _pageCancellation = new CancellationTokenSource();
        }

        return _catalogLoaded && _loadedCatalogSearch == CatalogSearch
            ? Task.CompletedTask
            : LoadCatalogAsync();
    }

    public void Deactivate()
    {
        CancelLifetime();
        _catalogSearchDelay?.Cancel();
        _catalogLoadCancellation?.Cancel();
        _patientSearchDelay?.Cancel();
        _patientLoadCancellation?.Cancel();
        _pageCancellation.Cancel();
    }

    protected override bool CanSave() => base.CanSave() && HasPatient && HasSelectedTests;

    protected override object BuildRequest() => new CreateOrderRequest
    {
        PatientId = IsNewPatient ? null : SelectedPatient?.Id,
        NewPatient = IsNewPatient ? PatientForm.ToCreateRequest() : null,
        Items = SelectedTests.Select(t => new CreateOrderItemRequest { LabTestId = t.Test.Id }).ToList(),
        OurPrice = OurPrice is { } price ? Money.ToMinor(price) : null,
        FirstPayment = PaymentAmount is { } amount
            ? new CreateFirstPaymentRequest
            {
                Amount = Money.ToMinor(amount),
                Method = PaymentMethod,
                Notes = string.IsNullOrWhiteSpace(PaymentNotes) ? null : PaymentNotes.Trim(),
            }
            : null,
    };

    protected override bool HasChanges(object request, object savedRequest) =>
        JsonSerializer.Serialize(request) != JsonSerializer.Serialize(savedRequest);

    /// <summary>The request's nested values: the attributes on the request itself do not look inside them.</summary>
    protected override void ValidateMore(FormErrors errors)
    {
        if (IsNewPatient && !PatientForm.ValidateInput())
        {
            errors.Set(nameof(CreateOrderRequest.NewPatient), ErrorCodes.Validation);
        }

        foreach (var test in SelectedTests)
        {
            test.Errors.Clear();
            var item = new CreateOrderItemRequest { LabTestId = test.Test.Id };
            foreach (var (field, code) in Check(item))
            {
                test.Errors.Set(field, code);
                errors.Set(nameof(CreateOrderRequest.Items), ErrorCodes.Validation);
            }
        }

        if (((CreateOrderRequest)BuildRequest()).FirstPayment is { } payment)
        {
            foreach (var (field, code) in Check(payment))
            {
                errors.Set(PaymentField(field), code);
            }

            if (errors[nameof(PaymentAmount)] is null && payment.Amount > Total)
            {
                errors.Set(nameof(PaymentAmount), ErrorCodes.ExceedsTotal);
            }
        }

        // Below the Compact breakpoint the field with the error may be on another tab.
        if (errors.HasErrors)
        {
            FormErrorKey = "ErrorCode.validation";
        }
    }

    /// <summary>"NewPatient.Phone" goes to the patient form, "Items[1].LabTestId" to that test, "FirstPayment.Amount" to the payment.</summary>
    protected override bool TrySetFieldError(string field, string code)
    {
        if (!SetFieldError(field, code))
        {
            return false;
        }

        FormErrorKey = "ErrorCode.validation";
        return true;
    }

    private bool SetFieldError(string field, string code)
    {
        var dot = field.IndexOf('.', StringComparison.Ordinal);
        if (dot < 0)
        {
            return base.TrySetFieldError(field, code);
        }

        var (owner, name) = (field[..dot], field[(dot + 1)..]);
        if (owner.Equals(nameof(CreateOrderRequest.NewPatient), StringComparison.OrdinalIgnoreCase))
        {
            PatientForm.Errors.Set(name, code);
            return true;
        }

        if (owner.Equals(nameof(CreateOrderRequest.FirstPayment), StringComparison.OrdinalIgnoreCase))
        {
            Errors.Set(PaymentField(name), code);
            return true;
        }

        if (owner.StartsWith(nameof(CreateOrderRequest.Items) + "[", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(owner[(nameof(CreateOrderRequest.Items).Length + 1)..^1], out var index)
            && index >= 0 && index < SelectedTests.Count)
        {
            SelectedTests[index].Errors.Set(name, code);
            return true;
        }

        return false;
    }

    protected override async Task<IApiResult> SubmitAsync(object request, CancellationToken cancellationToken)
    {
        var result = await _orders.CreateOrderAsync((CreateOrderRequest)request, cancellationToken);
        _created = result.Value;
        return result;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        switch (e.PropertyName)
        {
            case nameof(IsNewPatient) or nameof(SelectedPatient):
                InputChanged(e.PropertyName);
                break;
            case nameof(OurPrice):
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(MaxPayableAmount));
                OnPropertyChanged(nameof(Remaining));
                break;
            case nameof(PaymentAmount):
                OnPropertyChanged(nameof(HasPayment));
                OnPropertyChanged(nameof(Remaining));
                break;
            case nameof(CatalogSearch):
                ScheduleCatalogSearch();
                break;
            case nameof(PatientSearch):
                SchedulePatientSearch();
                break;
        }
    }

    [RelayCommand]
    private Task<bool> BackAsync() => _navigation.BackAsync();

    partial void OnPatientPickChanged(PatientDto? value)
    {
        if (value is not null)
        {
            SelectPatient(value);
        }
    }

    [RelayCommand]
    private void SelectPatient(PatientDto? patient)
    {
        if (patient is not null)
        {
            SelectedPatient = patient;
            PatientSearch = null;
            Patients = [];
            _loadedPatientSearch = null;
            Errors.Clear(nameof(CreateOrderRequest.PatientId));
        }
    }

    [RelayCommand]
    private void ChangePatient()
    {
        SelectedPatient = null;
        PatientPick = null;
        PatientSearch = null;
        Patients = [];
        _loadedPatientSearch = null;
    }

    /// <summary>Waits briefly so typing does not hit the API on every keystroke, then searches patients.</summary>
    private void SchedulePatientSearch()
    {
        _patientSearchDelay?.Cancel();
        var delay = new CancellationTokenSource();
        _patientSearchDelay = delay;
        _ = DebouncedPatientSearchAsync(delay.Token);
    }

    private async Task DebouncedPatientSearchAsync(CancellationToken delayToken)
    {
        try
        {
            await Task.Delay(PatientSearchDelay, delayToken);
        }
        catch (OperationCanceledException) when (delayToken.IsCancellationRequested)
        {
            return;
        }

        await SearchPatientsAsync();
    }

    private async Task SearchPatientsAsync()
    {
        var search = PatientSearch;
        if (string.IsNullOrWhiteSpace(search) || PageQuery.IsSearchTooShort(search))
        {
            _patientLoadCancellation?.Cancel();
            Patients = [];
            _loadedPatientSearch = search;
            IsLoadingPatients = false;
            OnPropertyChanged(nameof(NoPatientsFound));
            return;
        }

        _patientLoadCancellation?.Cancel();
        var load = CancellationTokenSource.CreateLinkedTokenSource(_pageCancellation.Token);
        _patientLoadCancellation = load;
        var cancellationToken = load.Token;

        IsLoadingPatients = true;
        try
        {
            var result = await _patients.SearchPatientsAsync(search, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!result.IsSuccess)
            {
                _toasts.Error(Localization.Get(result.Error == ApiError.Unreachable ? "Login.ServerUnreachable" : "Error.RequestFailed"));
                _loadedPatientSearch = search;
                Patients = [];
                return;
            }

            _loadedPatientSearch = search;
            Patients = result.Value!.Items;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer search started, or the user left the page.
        }
        finally
        {
            if (_patientLoadCancellation == load)
            {
                _patientLoadCancellation = null;
                IsLoadingPatients = false;
            }
        }
    }

    [RelayCommand]
    private void ChooseExistingPatient() => IsNewPatient = false;

    [RelayCommand]
    private void ChooseNewPatient() => IsNewPatient = true;

    [RelayCommand]
    private void AddTest(CatalogTestItem? item)
    {
        if (item is null || item.IsSelected)
        {
            return;
        }

        item.IsSelected = true;
        SelectedTests.Add(new OrderItemDraft(item));
    }

    [RelayCommand]
    private void RemoveTest(OrderItemDraft? draft)
    {
        if (draft is not null && SelectedTests.Remove(draft))
        {
            draft.Item.IsSelected = false;
        }
    }

    [RelayCommand]
    private Task RetryCatalogAsync() => LoadCatalogAsync();

    /// <summary>Waits briefly so typing does not hit the API on every keystroke, then reloads the visible catalog.</summary>
    private void ScheduleCatalogSearch()
    {
        _catalogSearchDelay?.Cancel();
        var delay = new CancellationTokenSource();
        _catalogSearchDelay = delay;
        _ = DebouncedCatalogSearchAsync(delay.Token);
    }

    private async Task DebouncedCatalogSearchAsync(CancellationToken delayToken)
    {
        try
        {
            await Task.Delay(CatalogSearchDelay, delayToken);
        }
        catch (OperationCanceledException) when (delayToken.IsCancellationRequested)
        {
            return;
        }

        await LoadCatalogAsync();
    }

    private async Task LoadCatalogAsync()
    {
        var search = CatalogSearch;
        _catalogLoadCancellation?.Cancel();
        var load = CancellationTokenSource.CreateLinkedTokenSource(_pageCancellation.Token);
        _catalogLoadCancellation = load;
        var cancellationToken = load.Token;

        IsCatalogLoading = true;
        try
        {
            var result = await _tests.GetCatalogAsync(search, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!result.IsSuccess)
            {
                CatalogLoadError = result.Error;
                return;
            }

            var selected = SelectedTests.Select(t => t.Test.Id).ToHashSet();
            Catalog = result.Value!.Items
                .Select(t => new CatalogTestItem(t) { IsSelected = selected.Contains(t.Id) })
                .ToList();
            CatalogLoadError = null;
            _catalogLoaded = true;
            _loadedCatalogSearch = search;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer search started, or the user left the page.
        }
        finally
        {
            if (_catalogLoadCancellation == load)
            {
                _catalogLoadCancellation = null;
                IsCatalogLoading = false;
            }
        }
    }

    private void InputChanged(string propertyName)
    {
        OnPropertyChanged(nameof(HasPatient));
        OnPropertyChanged(nameof(HasSelectedTests));
        OnPropertyChanged(nameof(CatalogTotal));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(MaxPayableAmount));
        OnPropertyChanged(nameof(Remaining));
        Errors.Clear(propertyName);
        SaveCommand.NotifyCanExecuteChanged();
    }

    private async void OnClosed(object? sender, bool saved)
    {
        if (!saved || _created is not { } order)
        {
            return;
        }

        OnOrderCreated?.Invoke();
        _toasts.Success(Localization.Format("Orders.Created", order.Number));
        await _navigation.OpenAsync<OrderDetailsViewModel>(page => page.Seed(order));
    }

    private static string PaymentField(string requestField) => requestField switch
    {
        nameof(CreateFirstPaymentRequest.Amount) => nameof(PaymentAmount),
        nameof(CreateFirstPaymentRequest.Method) => nameof(PaymentMethod),
        nameof(CreateFirstPaymentRequest.Notes) => nameof(PaymentNotes),
        _ => requestField,
    };

    private static IEnumerable<(string Field, string Code)> Check(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results.SelectMany(r => r.MemberNames.Select(field => (field, r.ErrorMessage ?? ErrorCodes.Invalid)));
    }
}

/// <summary>A card of the catalog column. Selected tests stay in the grid as a disabled ghost.</summary>
public partial class CatalogTestItem(LabTestDto test) : ObservableObject
{
    public LabTestDto Test { get; } = test;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>A selected test; its catalog price contributes to the order total.</summary>
public partial class OrderItemDraft(CatalogTestItem item) : ObservableObject
{
    public CatalogTestItem Item { get; } = item;

    public LabTestDto Test => Item.Test;

    /// <summary>Errors of this item, keyed like CreateOrderRequest item fields (LabTestId).</summary>
    public FormErrors Errors { get; } = new();
}
