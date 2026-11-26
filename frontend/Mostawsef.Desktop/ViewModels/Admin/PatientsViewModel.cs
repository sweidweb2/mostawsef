using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Patients;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.Services.Printing;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Admin;

/// <summary>
/// Admin portal: patients as a table or a grid of cards, with search (name or phone) and paging; add, edit, delete,
/// and export of every patient as a PDF.
/// </summary>
public partial class PatientsViewModel : NumberedListViewModel<PatientDto>
{
    public const int PatientsPerPage = 40;

    private readonly PatientsApi _patients;
    private readonly IDialogService _dialogs;
    private readonly PrintService _print;
    private readonly SavePathPicker _pickSavePath;
    private readonly ErrorReporter _errors;
    private readonly NavigationService _navigation;

    /// <summary>One Patient details page for this portal visit; pointed at a patient on each open so PageHost can reuse its view.</summary>
    private PatientDetailsViewModel? _details;

    public PatientsViewModel(
        PatientsApi patients,
        IDialogService dialogs,
        IToastService toasts,
        LocalizationService localization,
        PrintService print,
        SavePathPicker pickSavePath,
        NavigationService navigation,
        ErrorReporter errors)
        : base(toasts, localization, errors)
    {
        _navigation = navigation;

        // An order was created or paid: a patient's unpaid tint may be out of date, so the list reloads when it is shown again.
        WeakReferenceMessenger.Default.Register<PatientsViewModel, OrderChangedMessage>(this, (page, _) => page.OnOrderChanged());
        _patients = patients;
        _dialogs = dialogs;
        _print = print;
        _pickSavePath = pickSavePath;
        _errors = errors;
        PageSize = PatientsPerPage;
    }

    [ObservableProperty]
    public partial bool IsGridView { get; private set; }

    protected override Task<ApiResult<PagedResult<PatientDto>>> FetchPageAsync(
        int page, int pageSize, string? search, bool includeTotal, CancellationToken cancellationToken) =>
        _patients.GetPatientsAsync(page, pageSize, search, includeTotal, includeUnpaid: true, cancellationToken);

    /// <summary>An order changed while this list was loaded: reload on return, but keep the count (orders never add or remove patients).</summary>
    private bool _keepTotalOnReload;

    private void OnOrderChanged()
    {
        _keepTotalOnReload = IsLoaded;
        MarkStale();
    }

    protected override Task LoadOnActivateAsync()
    {
        var keepTotal = _keepTotalOnReload;
        _keepTotalOnReload = false;
        return ReloadAsync(includeTotal: !keepTotal);
    }

    /// <summary>Opens the patient's details page (their info and orders).</summary>
    [RelayCommand]
    private Task OpenAsync(PatientDto? patient)
    {
        if (patient is null)
        {
            return Task.CompletedTask;
        }

        return _navigation.Reuse(ref _details) is { } details
            ? _navigation.OpenAsync(details, page => page.Show(patient))
            : Task.CompletedTask;
    }

    [RelayCommand]
    private void ShowGrid() => IsGridView = true;

    [RelayCommand]
    private void ShowTable() => IsGridView = false;

    [RelayCommand]
    private async Task AddAsync()
    {
        var form = new PatientFormViewModel(_patients, _dialogs, Localization);
        if (!await _dialogs.ShowFormAsync(form, Localization.Get("Patients.AddTitle"), 640))
        {
            return;
        }

        Toasts.Success(Localization.Get("Patients.Saved"));
        if (form.SavedValue is PatientDto created)
        {
            InsertSorted(created, CompareByName);
        }
        else
        {
            await ReloadAsync();
        }
    }

    [RelayCommand]
    private async Task EditAsync(PatientDto? item)
    {
        if (item is null)
        {
            return;
        }

        // List rows leave the notes out; the form is filled from the full record so saving does not wipe them.
        if (await ReadAsync(ct => _patients.GetPatientAsync(item.Id, ct)) is not { } loaded)
        {
            return;
        }

        if (!loaded.IsSuccess)
        {
            ShowError(loaded);
            return;
        }

        var form = new PatientFormViewModel(_patients, _dialogs, Localization, loaded.Value!);
        if (await _dialogs.ShowFormAsync(form, Localization.Get("Patients.EditTitle"), 640))
        {
            Toasts.Success(Localization.Get("Patients.Saved"));
            if (form.SavedValue is PatientDto saved)
            {
                // List rows leave notes out; keep the unpaid flag from the row that was edited.
                ReplaceSorted(
                    saved with { Notes = null, HasUnpaidOrders = item.HasUnpaidOrders },
                    p => p.Id,
                    CompareByName);
            }
            else
            {
                await ReloadAsync(includeTotal: false);
            }
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(PatientDto? item)
    {
        if (item is null
            || !await _dialogs.ConfirmAsync(
                Localization.Format("Patients.DeleteConfirm", item.FullName), confirmText: Localization.Get("Common.Delete"), isDanger: true))
        {
            return;
        }

        if (await ReadAsync(ct => _patients.DeletePatientAsync(item.Id, ct)) is not { } result)
        {
            return;
        }

        if (!result.IsSuccess)
        {
            ShowError(result);
            return;
        }

        Toasts.Success(Localization.Get("Patients.Deleted"));
        await ReloadAsync();
    }

    /// <summary>True while the export is fetching and writing the PDF (its button shows a spinner).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    public partial bool IsExporting { get; private set; }

    private bool CanExport() => !IsExporting;

    /// <summary>Asks where to save, then writes up to <see cref="PageQuery.MaxExportRows"/> patients there as a PDF.</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        string? path;
        try
        {
            path = await _pickSavePath("patients.pdf", Localization.Get("Print.SaveTitle"));
        }
        catch (Exception ex)
        {
            _errors.Report(ex, context: "Choosing where to save the patients export");
            Toasts.Error(Localization.Get("Print.Failed"));
            return;
        }

        if (path is null)
        {
            return;
        }

        IsExporting = true;
        try
        {
            if (await ReadAsync(ct => _patients.GetAllPatientsAsync(ct)) is not { } result)
            {
                return;
            }

            if (!result.IsSuccess)
            {
                ShowError(result);
                return;
            }

            var rows = result.Value!;
            if (rows.Count >= PageQuery.MaxExportRows)
            {
                Toasts.Show(Localization.Get("Print.ExportTruncated"), kind: ToastKind.Warning);
            }

            await _print.SaveAsync(new PatientsDocument(await _print.CreateContextAsync(), rows), path);
            Toasts.Success(Localization.Format("Print.Exported", path));
        }
        catch (Exception ex)
        {
            _errors.Report(ex, context: "Exporting the patients");
            Toasts.Error(Localization.Get("Print.Failed"));
        }
        finally
        {
            IsExporting = false;
        }
    }

    private static int CompareByName(PatientDto a, PatientDto b)
    {
        var byName = string.Compare(a.FullName, b.FullName, StringComparison.OrdinalIgnoreCase);
        return byName != 0 ? byName : a.Id.CompareTo(b.Id);
    }
}
