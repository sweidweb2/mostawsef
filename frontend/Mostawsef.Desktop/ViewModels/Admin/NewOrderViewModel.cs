using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Contracts.Orders;
using Mostawsef.Contracts.Patients;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Admin;

/// <summary>
/// Admin portal sub-page: a new order in three steps (patient, tests, review). Saving sends one request;
/// the API saves it all or nothing. Orders reuses one instance per portal visit; <see cref="BeginReuse"/>
/// clears the form. The lab-test catalog loads only when the tests step is shown; it comes from the shared
/// <see cref="LabTestCatalog"/> cache and is searched in memory.
/// </summary>
public partial class NewOrderViewModel : FormViewModel, ILoadablePage, IReusablePage, IBreadcrumbPage
{
    public string BreadcrumbKey => "NewOrder.Title";

    public string? BreadcrumbArg => null;

    public const int PatientStep = 0;
    public const int TestsStep = 1;
    public const int ReviewStep = 2;

    private readonly OrdersApi _orders;
    private readonly PatientsApi _patients;
    private readonly LabTestCatalog _catalog;
    private readonly LabTestCatalogVersion _catalogVersion;
    private readonly NavigationService _navigation;
    private readonly IToastService _toasts;
    private readonly ErrorReporter _errors;

    private readonly PageLifetime _pageLifetime = new();
    private CancellationTokenSource? _catalogLoadCancellation;
    private CancellationTokenSource? _patientLoadCancellation;

    /// <summary>Every active test (unfiltered); <see cref="Catalog"/> is the part matching <see cref="CatalogSearch"/>.</summary>
    private List<CatalogTestItem>? _allCatalog;
    private int _loadedCatalogVersion;
    private string? _loadedPatientSearch;
    private OrderDetailsDto? _created;
    private bool _patientIncomplete;
    private bool _testsIncomplete;

    /// <summary>Called after a successful create so the orders list can insert the row without a full reload.</summary>
    public Action<OrderDetailsDto>? OnOrderCreated { get; set; }

    /// <summary>Shared Order details instance from Orders so PageHost reuses one view after create.</summary>
    public OrderDetailsViewModel? OrderDetailsPage { get; set; }

    public NewOrderViewModel(
        OrdersApi orders,
        PatientsApi patients,
        LabTestCatalog catalog,
        LabTestCatalogVersion catalogVersion,
        NavigationService navigation,
        IDialogService dialogs,
        IToastService toasts,
        ErrorReporter errors,
        LocalizationService localization)
        : base(dialogs, localization)
    {
        _orders = orders;
        _patients = patients;
        _catalog = catalog;
        _catalogVersion = catalogVersion;
        _navigation = navigation;
        _toasts = toasts;
        _errors = errors;

        PatientForm = new PatientFormViewModel(patients, dialogs, localization);
        PatientForm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not (nameof(IsDirty) or nameof(IsBusy) or nameof(FormErrorKey) or nameof(HasFormError)))
            {
                NotifyPatientInputsChanged(nameof(PatientForm));
            }
        };
        SelectedTests.CollectionChanged += (_, _) => NotifyTestsInputsChanged();
        Closed += OnClosed;
        _patientStep = new NewOrderPatientStep(this);
        _testsStep = new NewOrderTestsStep(this);
        _reviewStep = new NewOrderReviewStep(this);
        Steps =
        [
            new OrderStepItem(PatientStep, "1", "NewOrder.StepPatient", hasLineAfter: true),
            new OrderStepItem(TestsStep, "2", "NewOrder.StepTests", hasLineAfter: true),
            new OrderStepItem(ReviewStep, "3", "NewOrder.StepReview", hasLineAfter: false),
        ];
        RefreshStepContent();
        RefreshStepStatuses();

        MarkClean();
    }

    private readonly NewOrderPatientStep _patientStep;
    private readonly NewOrderTestsStep _testsStep;
    private readonly NewOrderReviewStep _reviewStep;

    /// <summary>Patient, tests, review — one item each for the step indicator template.</summary>
    public IReadOnlyList<OrderStepItem> Steps { get; }

    /// <summary>First connector line (patient → tests).</summary>
    public OrderStepItem PatientStepItem => Steps[PatientStep];

    /// <summary>Second connector line (tests → review).</summary>
    public OrderStepItem TestsStepItem => Steps[TestsStep];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPatientStep), nameof(IsTestsStep), nameof(IsReviewStep), nameof(CanGoBack), nameof(ShowNext), nameof(ShowSave))]
    [NotifyCanExecuteChangedFor(nameof(PreviousStepCommand))]
    public partial int SelectedStep { get; set; }

    /// <summary>Active step marker for <c>CachedContentHost</c> (views are cached per step instance).</summary>
    [ObservableProperty]
    public partial object? StepContent { get; private set; }

    public bool IsPatientStep => SelectedStep == PatientStep;

    public bool IsTestsStep => SelectedStep == TestsStep;

    public bool IsReviewStep => SelectedStep == ReviewStep;

    public bool CanGoBack => !IsPreparing && SelectedStep > PatientStep;

    public bool ShowNext => !IsPreparing && SelectedStep < ReviewStep;

    public bool ShowSave => !IsPreparing && SelectedStep == ReviewStep;

    /// <summary>Patient step is filled (existing pick or new-patient required fields without errors).</summary>
    public bool IsPatientSatisfied =>
        IsNewPatient
            ? PatientForm.HasRequiredFields && !PatientForm.Errors.HasErrors
            : SelectedPatient is not null;

    /// <summary>At least one test is on the order.</summary>
    public bool IsTestsSatisfied => HasSelectedTests;

    public PatientFormViewModel PatientForm { get; }

    /// <summary>
    /// Null until new-patient mode is first chosen (most orders never need the form), then kept: the view only shows
    /// or hides it, because rebuilding the form on every Existing/New switch cost 35-90 ms each time.
    /// </summary>
    public PatientFormViewModel? NewPatientFormContent => _newPatientFormBuilt ? PatientForm : null;

    private bool _newPatientFormBuilt;

    /// <summary>True while the page is resetting and loading the catalog after navigation.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNext), nameof(ShowSave), nameof(CanGoBack))]
    [NotifyCanExecuteChangedFor(nameof(PreviousStepCommand))]
    public partial bool IsPreparing { get; private set; }

    [ObservableProperty]
    public partial bool IsNewPatient { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoPatientsFound))]
    public partial IReadOnlyList<PatientDto> Patients { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoPatientsFound))]
    public partial bool IsLoadingPatients { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoPatientsFound))]
    public partial string? PatientSearch { get; set; }

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

    public bool HasPatient => IsNewPatient ? PatientForm.HasRequiredFields : SelectedPatient is not null;

    public IReadOnlyList<string> PaymentMethodOptions => PaymentMethods.All;

    [ObservableProperty]
    public partial decimal? PaymentAmount { get; set; }

    [ObservableProperty]
    public partial string PaymentMethod { get; set; } = PaymentMethods.Cash;

    [ObservableProperty]
    public partial string PaymentNotes { get; set; } = string.Empty;

    public bool HasPayment => PaymentAmount is > 0;

    public ObservableCollection<OrderItemDraft> SelectedTests { get; } = [];

    public bool HasSelectedTests => SelectedTests.Count > 0;

    public decimal MaxAddon { get; } = Money.ToMajor(OrderRules.MaxAddon);

    private long _catalogTotal;

    public long CatalogTotal => _catalogTotal;

    [ObservableProperty]
    public partial decimal? Addon { get; set; }

    public long Total => _catalogTotal + (Addon is { } price ? Money.ToMinor(price) : 0);

    public decimal MaxPayableAmount => Money.ToMajor(Math.Min(Total, OrderRules.MaxPaymentAmount));

    public long Remaining => Total - (PaymentAmount is { } amount ? Money.ToMinor(amount) : 0);

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

    public bool IsCatalogEmpty => !IsCatalogLoading && !HasCatalogError && Catalog.Count == 0;

    /// <summary>Resets a reused page and shows the preparing overlay before <see cref="ActivateAsync"/> loads data.</summary>
    public void BeginReuse() => ResetCore();

    public async Task ActivateAsync()
    {
        EnsureLifetime();
        _pageLifetime.EnsureActive();

        try
        {
            if (SelectedStep == TestsStep)
            {
                await EnsureCatalogAsync();
            }
        }
        finally
        {
            FinishPreparing();
        }
    }

    public void Deactivate()
    {
        CancelInFlightWork();
        _pageLifetime.Cancel();
        IsPreparing = false;
        Catalog = [];
        _allCatalog = null;
        _created = null;
    }

    /// <summary>Cancels work, clears form state, and shows the preparing overlay for the next paint.</summary>
    private void ResetCore()
    {
        CancelInFlightWork();
        _pageLifetime.Cancel();
        _pageLifetime.EnsureActive();
        EnsureLifetime();

        _created = null;
        IsPreparing = true;
        SelectedStep = PatientStep;
        IsNewPatient = false;
        SelectedPatient = null;
        PatientPick = null;
        PatientSearch = null;
        Patients = [];
        _loadedPatientSearch = null;
        IsLoadingPatients = false;
        PatientForm.Reset();
        _patientIncomplete = false;
        _testsIncomplete = false;
        SelectedTests.Clear();
        foreach (var item in _allCatalog ?? [])
        {
            item.IsSelected = false;
        }

        CatalogSearch = null;
        CatalogLoadError = null;
        Addon = null;
        PaymentAmount = null;
        PaymentMethod = PaymentMethods.Cash;
        PaymentNotes = string.Empty;
        FormErrorKey = null;
        Errors.Clear();
        RefreshStepContent();
        RefreshStepStatuses();
        MarkClean();
        SaveCommand.NotifyCanExecuteChanged();
    }

    private void CancelInFlightWork()
    {
        CancelLifetime();
        _catalogLoadCancellation?.Cancel();
        _patientLoadCancellation?.Cancel();
    }

    private void FinishPreparing()
    {
        if (!IsPreparing)
        {
            return;
        }

        IsPreparing = false;
        RefreshStepContent();
        RefreshStepStatuses();
        SaveCommand.NotifyCanExecuteChanged();
    }

    protected override bool CanSave() => base.CanSave() && IsReviewStep;

    protected override object BuildRequest() => new CreateOrderRequest
    {
        PatientId = IsNewPatient ? null : SelectedPatient?.Id,
        NewPatient = IsNewPatient ? PatientForm.ToCreateRequest() : null,
        Items = SelectedTests.Select(t => new CreateOrderItemRequest { LabTestId = t.Test.Id }).ToList(),
        Addon = Addon is { } price ? Money.ToMinor(price) : null,
        FirstPayment = PaymentAmount is { } amount
            ? new CreateFirstPaymentRequest
            {
                Amount = Money.ToMinor(amount),
                Method = PaymentMethod,
                Notes = string.IsNullOrWhiteSpace(PaymentNotes) ? null : PaymentNotes.Trim(),
            }
            : null,
    };

    /// <summary>
    /// The inputs of this form are not named like the request fields. Patient and test changes call RefreshDirty from
    /// NotifyPatientInputsChanged / NotifyTestsInputsChanged; step, catalog and search changes never touch the request.
    /// </summary>
    protected override bool AffectsRequest(string propertyName) =>
        propertyName is nameof(Addon) or nameof(PaymentAmount) or nameof(PaymentMethod) or nameof(PaymentNotes);

    /// <summary>Items is a list (reference equality), so compare field-by-field instead of JSON.</summary>
    protected override bool HasChanges(object request, object savedRequest)
    {
        var current = (CreateOrderRequest)request;
        var saved = (CreateOrderRequest)savedRequest;
        return current.PatientId != saved.PatientId
            || current.Addon != saved.Addon
            || !Equals(current.NewPatient, saved.NewPatient)
            || !Equals(current.FirstPayment, saved.FirstPayment)
            || !SameItems(current.Items, saved.Items);
    }

    private static bool SameItems(IReadOnlyList<CreateOrderItemRequest> current, IReadOnlyList<CreateOrderItemRequest> saved)
    {
        if (current.Count != saved.Count)
        {
            return false;
        }

        for (var i = 0; i < current.Count; i++)
        {
            if (current[i].LabTestId != saved[i].LabTestId)
            {
                return false;
            }
        }

        return true;
    }

    protected override void ValidateMore(FormErrors errors)
    {
        ApplyPatientStepErrors(errors);
        ApplyTestsStepErrors(errors);

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

        if (errors.HasErrors)
        {
            FormErrorKey = "ErrorCode.validation";
            JumpToErrorStep(errors);
        }

        RefreshStepStatuses();
    }

    protected override bool TrySetFieldError(string field, string code)
    {
        if (!SetFieldError(field, code))
        {
            return false;
        }

        FormErrorKey = "ErrorCode.validation";
        JumpToFieldStep(field);
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
                NotifyPatientInputsChanged(e.PropertyName);
                break;
            case nameof(Addon):
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(MaxPayableAmount));
                OnPropertyChanged(nameof(Remaining));
                break;
            case nameof(PaymentAmount):
                OnPropertyChanged(nameof(HasPayment));
                OnPropertyChanged(nameof(Remaining));
                break;
            case nameof(CatalogSearch):
                ApplyCatalogSearch();
                break;
            case nameof(SelectedStep):
                RefreshStepContent();
                RefreshStepStatuses();
                if (SelectedStep == TestsStep)
                {
                    _ = EnsureCatalogAsync();
                }

                SaveCommand.NotifyCanExecuteChanged();
                break;
        }
    }

    [RelayCommand]
    private Task<bool> BackAsync() => _navigation.BackAsync();

    [RelayCommand]
    private void NextStep()
    {
        if (SelectedStep < ReviewStep)
        {
            MoveToStep(SelectedStep + 1);
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void PreviousStep()
    {
        if (SelectedStep > PatientStep)
        {
            MoveToStep(SelectedStep - 1);
        }
    }

    [RelayCommand]
    private void GoToStep(object? step)
    {
        if (step is int index)
        {
            MoveToStep(index);
        }
    }

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

    /// <summary>
    /// Loads matches for the patient AutoCompleteBox (Avalonia calls this via AsyncPopulator so the
    /// drop-down opens after the API returns). Also used by tests.
    /// </summary>
    public async Task<IReadOnlyList<PatientDto>> SearchPatientsAsync(string? search, CancellationToken cancellationToken = default)
    {
        PatientSearch = search;
        if (string.IsNullOrWhiteSpace(search) || PageQuery.IsSearchTooShort(search))
        {
            _patientLoadCancellation?.Cancel();
            Patients = [];
            _loadedPatientSearch = search;
            IsLoadingPatients = false;
            OnPropertyChanged(nameof(NoPatientsFound));
            return Patients;
        }

        _patientLoadCancellation?.Cancel();
        var load = CancellationTokenSource.CreateLinkedTokenSource(_pageLifetime.Token, cancellationToken);
        _patientLoadCancellation = load;
        var token = load.Token;

        IsLoadingPatients = true;
        try
        {
            var result = await _patients.SearchPatientsAsync(search, token);
            if (token.IsCancellationRequested)
            {
                return Patients;
            }

            if (!result.IsSuccess)
            {
                _toasts.Error(Localization.Get(result.Error == ApiError.Unreachable ? "Login.ServerUnreachable" : "Error.RequestFailed"));
                _loadedPatientSearch = search;
                Patients = [];
                return Patients;
            }

            _loadedPatientSearch = search;
            Patients = result.Value!.Items;
            return Patients;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return Patients;
        }
        finally
        {
            if (_patientLoadCancellation == load)
            {
                _patientLoadCancellation = null;
                IsLoadingPatients = false;
            }

            load.Dispose();
        }
    }

    [RelayCommand]
    private void ChooseExistingPatient() => IsNewPatient = false;

    [RelayCommand]
    private void ChooseNewPatient() => IsNewPatient = true;

    partial void OnIsNewPatientChanged(bool value)
    {
        if (value && !_newPatientFormBuilt)
        {
            _newPatientFormBuilt = true;
            OnPropertyChanged(nameof(NewPatientFormContent));
        }
    }

    [RelayCommand]
    private void AddTest(CatalogTestItem? item)
    {
        if (item is null || item.IsSelected || SelectedTests.Count >= OrderRules.MaxItems)
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

    private Task EnsureCatalogAsync() =>
        _allCatalog is not null && _loadedCatalogVersion == _catalogVersion.Current
            ? Task.CompletedTask
            : LoadCatalogAsync();

    /// <summary>Gets the catalog (from the shared cache when it is current), then shows the part matching the search.</summary>
    private async Task LoadCatalogAsync()
    {
        _catalogLoadCancellation?.Cancel();
        var load = CancellationTokenSource.CreateLinkedTokenSource(_pageLifetime.Token);
        _catalogLoadCancellation = load;
        var cancellationToken = load.Token;

        var version = _catalogVersion.Current;
        IsCatalogLoading = true;
        try
        {
            var result = await _catalog.GetAsync(cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!result.IsSuccess)
            {
                CatalogLoadError = result.Error;
                return;
            }

            _allCatalog = BuildCatalog(result.Value!);
            _loadedCatalogVersion = version;
            CatalogLoadError = null;
            ApplyCatalogSearch();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _errors.Report(ex, context: "Loading lab test catalog");
            CatalogLoadError = ApiError.Server;
        }
        finally
        {
            if (_catalogLoadCancellation == load)
            {
                _catalogLoadCancellation = null;
                IsCatalogLoading = false;
            }

            load.Dispose();
        }
    }

    /// <summary>Filters the loaded catalog in memory: instant, and no request per keystroke.</summary>
    private void ApplyCatalogSearch()
    {
        if (_allCatalog is not { } all)
        {
            return;
        }

        var term = SearchText.Normalize(CatalogSearch);
        if (term.Length == 0)
        {
            Catalog = all;
            return;
        }

        var matches = new List<CatalogTestItem>();
        foreach (var item in all)
        {
            if (item.Matches(term))
            {
                matches.Add(item);
            }
        }

        Catalog = matches;
    }

    private void NotifyPatientInputsChanged(string propertyName)
    {
        OnPropertyChanged(nameof(HasPatient));
        OnPropertyChanged(nameof(IsPatientSatisfied));
        Errors.Clear(propertyName);
        if (IsPatientSatisfied)
        {
            Errors.Clear(nameof(CreateOrderRequest.PatientId));
        }

        RefreshStepStatuses();
        SaveCommand.NotifyCanExecuteChanged();
        RefreshDirty();
    }

    private void NotifyTestsInputsChanged()
    {
        long total = 0;
        foreach (var test in SelectedTests)
        {
            total += test.Test.Price;
        }

        _catalogTotal = total;
        OnPropertyChanged(nameof(HasSelectedTests));
        OnPropertyChanged(nameof(IsTestsSatisfied));
        OnPropertyChanged(nameof(CatalogTotal));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(MaxPayableAmount));
        OnPropertyChanged(nameof(Remaining));
        Errors.Clear(nameof(SelectedTests));
        if (IsTestsSatisfied)
        {
            Errors.Clear(nameof(CreateOrderRequest.Items));
        }

        RefreshStepStatuses();
        SaveCommand.NotifyCanExecuteChanged();
        RefreshDirty();
    }

    private List<CatalogTestItem> BuildCatalog(IReadOnlyList<LabTestDto> incoming)
    {
        var selected = new HashSet<Guid>(SelectedTests.Count);
        foreach (var draft in SelectedTests)
        {
            selected.Add(draft.Test.Id);
        }

        Dictionary<Guid, CatalogTestItem>? prior = null;
        if (_allCatalog is { Count: > 0 } existing)
        {
            prior = new Dictionary<Guid, CatalogTestItem>(existing.Count);
            foreach (var item in existing)
            {
                prior[item.Test.Id] = item;
            }
        }

        var list = new List<CatalogTestItem>(incoming.Count);
        foreach (var test in incoming)
        {
            var isSelected = selected.Contains(test.Id);
            if (prior is not null && prior.TryGetValue(test.Id, out var keep) && keep.Test.Equals(test))
            {
                keep.IsSelected = isSelected;
                list.Add(keep);
            }
            else
            {
                list.Add(new CatalogTestItem(test) { IsSelected = isSelected });
            }
        }

        return list;
    }

    private void MoveToStep(int step)
    {
        if (step is < PatientStep or > ReviewStep || step == SelectedStep)
        {
            return;
        }

        MarkLeavingStep(SelectedStep);
        SelectedStep = step;
    }

    private void MarkLeavingStep(int step)
    {
        switch (step)
        {
            case PatientStep:
                _patientIncomplete = true;
                ApplyPatientStepErrors(Errors);
                break;
            case TestsStep:
                _testsIncomplete = true;
                ApplyTestsStepErrors(Errors);
                break;
        }
    }

    private void ApplyPatientStepErrors(FormErrors errors)
    {
        Errors.Clear(nameof(CreateOrderRequest.PatientId));
        if (IsNewPatient)
        {
            if (!PatientForm.ValidateInput())
            {
                _patientIncomplete = true;
                errors.Set(nameof(CreateOrderRequest.NewPatient), ErrorCodes.Validation);
            }

            return;
        }

        if (SelectedPatient is null)
        {
            _patientIncomplete = true;
            errors.Set(nameof(CreateOrderRequest.PatientId), ErrorCodes.Required);
        }
    }

    private void ApplyTestsStepErrors(FormErrors errors)
    {
        if (HasSelectedTests)
        {
            Errors.Clear(nameof(CreateOrderRequest.Items));
            return;
        }

        _testsIncomplete = true;
        errors.Set(nameof(CreateOrderRequest.Items), ErrorCodes.Required);
    }

    private void RefreshStepStatuses()
    {
        var patient = StepStatus(_patientIncomplete, IsPatientSatisfied, IsPatientStep);
        var tests = StepStatus(_testsIncomplete, IsTestsSatisfied, IsTestsStep);
        var review = !IsReviewStep && IsPatientSatisfied && IsTestsSatisfied
            ? OrderStepStatus.Complete
            : OrderStepStatus.Number;

        Steps[PatientStep].Update(IsPatientStep, patient, lineBeforeComplete: false, lineComplete: IsPatientSatisfied);
        Steps[TestsStep].Update(IsTestsStep, tests, lineBeforeComplete: IsPatientSatisfied, lineComplete: IsTestsSatisfied);
        Steps[ReviewStep].Update(IsReviewStep, review, lineBeforeComplete: IsTestsSatisfied, lineComplete: false);
    }

    private static OrderStepStatus StepStatus(bool incomplete, bool satisfied, bool current) =>
        incomplete && !satisfied
            ? OrderStepStatus.Missing
            : satisfied && !current
                ? OrderStepStatus.Complete
                : OrderStepStatus.Number;

    private void RefreshStepContent() =>
        StepContent = IsPreparing
            ? null
            : SelectedStep switch
            {
                PatientStep => _patientStep,
                TestsStep => _testsStep,
                _ => _reviewStep,
            };

    private void JumpToErrorStep(FormErrors errors)
    {
        if (errors[nameof(CreateOrderRequest.NewPatient)] is not null
            || errors[nameof(CreateOrderRequest.PatientId)] is not null)
        {
            SelectedStep = PatientStep;
            return;
        }

        if (errors[nameof(CreateOrderRequest.Items)] is not null)
        {
            SelectedStep = TestsStep;
        }
    }

    private void JumpToFieldStep(string field)
    {
        // Prefer the earliest step when several server field errors arrive together.
        var step = field.StartsWith(nameof(CreateOrderRequest.NewPatient), StringComparison.OrdinalIgnoreCase)
            || field.Equals(nameof(CreateOrderRequest.PatientId), StringComparison.OrdinalIgnoreCase)
            ? PatientStep
            : field.StartsWith(nameof(CreateOrderRequest.Items), StringComparison.OrdinalIgnoreCase)
                ? TestsStep
                : field.StartsWith(nameof(CreateOrderRequest.FirstPayment), StringComparison.OrdinalIgnoreCase)
                    || field is nameof(PaymentAmount) or nameof(PaymentMethod) or nameof(PaymentNotes) or nameof(Addon)
                    ? ReviewStep
                    : SelectedStep;

        if (step < SelectedStep)
        {
            SelectedStep = step;
        }
    }

    private async void OnClosed(object? sender, bool saved)
    {
        if (!saved || _created is not { } order)
        {
            return;
        }

        try
        {
            OnOrderCreated?.Invoke(order);
            _toasts.Success(Localization.Format("Orders.Created", order.Number));
            if (OrderDetailsPage is { } details)
            {
                await _navigation.OpenAsync(details, page => page.Seed(order));
            }
            else
            {
                await _navigation.OpenAsync<OrderDetailsViewModel>(page => page.Seed(order));
            }
        }
        catch (Exception ex)
        {
            _errors.Report(ex, context: "Opening order after create");
        }
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
