using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Patients;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;
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
    private readonly ErrorReporter _errors;

    public PatientsViewModel(
        PatientsApi patients,
        IDialogService dialogs,
        IToastService toasts,
        LocalizationService localization,
        PrintService print,
        ErrorReporter errors)
        : base(toasts, localization)
    {
        _patients = patients;
        _dialogs = dialogs;
        _print = print;
        _errors = errors;
        PageSize = PatientsPerPage;
    }

    [ObservableProperty]
    public partial bool IsGridView { get; private set; }

    protected override Task<ApiResult<PagedResult<PatientDto>>> FetchPageAsync(
        int page, int pageSize, string? search, CancellationToken cancellationToken) =>
        _patients.GetPatientsAsync(page, pageSize, search, cancellationToken);

    [RelayCommand]
    private void ShowGrid() => IsGridView = true;

    [RelayCommand]
    private void ShowTable() => IsGridView = false;

    [RelayCommand]
    private async Task AddAsync()
    {
        if (await _dialogs.ShowFormAsync(new PatientFormViewModel(_patients, _dialogs, Localization), Localization.Get("Patients.AddTitle"), 640))
        {
            Toasts.Success(Localization.Get("Patients.Saved"));
            await ReloadAsync();
        }
    }

    [RelayCommand]
    private async Task EditAsync(PatientDto? item)
    {
        if (item is null || await ReadAsync(ct => _patients.GetPatientAsync(item.Id, ct)) is not { } result)
        {
            return;
        }

        if (!result.IsSuccess)
        {
            ShowError(result);
            return;
        }

        if (await _dialogs.ShowFormAsync(
                new PatientFormViewModel(_patients, _dialogs, Localization, result.Value), Localization.Get("Patients.EditTitle"), 640))
        {
            Toasts.Success(Localization.Get("Patients.Saved"));
            await ReloadAsync();
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

    /// <summary>All patients, whatever the search or page shown, as a PDF opened in the default viewer.</summary>
    [RelayCommand]
    private async Task ExportAsync()
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

        try
        {
            await _print.PrintAsync(new PatientsDocument(_print.CreateContext(), result.Value!), "patients");
        }
        catch (Exception ex)
        {
            _errors.Report(ex, context: "Exporting the patients");
            Toasts.Error(Localization.Get("Print.Failed"));
        }
    }
}
