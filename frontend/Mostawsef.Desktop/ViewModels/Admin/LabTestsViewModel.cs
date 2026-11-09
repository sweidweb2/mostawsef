using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Printing;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Admin;

/// <summary>
/// Admin portal: the lab test catalog as a table or a grid of cards, with search and paging; add, edit, delete,
/// and export of the whole catalog as a PDF.
/// </summary>
public partial class LabTestsViewModel : NumberedListViewModel<LabTestDto>
{
    public const int TestsPerPage = 40;

    private readonly LabTestsApi _tests;
    private readonly IDialogService _dialogs;
    private readonly PrintService _print;
    private readonly ErrorReporter _errors;

    public LabTestsViewModel(
        LabTestsApi tests,
        IDialogService dialogs,
        IToastService toasts,
        LocalizationService localization,
        PrintService print,
        ErrorReporter errors)
        : base(toasts, localization)
    {
        _tests = tests;
        _dialogs = dialogs;
        _print = print;
        _errors = errors;
        PageSize = TestsPerPage;
    }

    [ObservableProperty]
    public partial bool IsGridView { get; private set; }

    protected override Task<ApiResult<PagedResult<LabTestDto>>> FetchPageAsync(
        int page, int pageSize, string? search, bool includeTotal, CancellationToken cancellationToken) =>
        _tests.GetTestsAsync(page, pageSize, search, includeTotal: includeTotal, cancellationToken: cancellationToken);

    [RelayCommand]
    private void ShowGrid() => IsGridView = true;

    [RelayCommand]
    private void ShowTable() => IsGridView = false;

    [RelayCommand]
    private async Task AddAsync()
    {
        if (await _dialogs.ShowFormAsync(new LabTestFormViewModel(_tests, _dialogs, Localization), Localization.Get("LabTests.AddTitle")))
        {
            Toasts.Success(Localization.Get("LabTests.Saved"));
            await ReloadAsync();
        }
    }

    [RelayCommand]
    private async Task EditAsync(LabTestDto? item)
    {
        // List rows are already full LabTestDto (including Version); no GET needed.
        if (item is null)
        {
            return;
        }

        if (await _dialogs.ShowFormAsync(new LabTestFormViewModel(_tests, _dialogs, Localization, item), Localization.Get("LabTests.EditTitle")))
        {
            Toasts.Success(Localization.Get("LabTests.Saved"));
            await ReloadAsync(includeTotal: false);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(LabTestDto? item)
    {
        if (item is null
            || !await _dialogs.ConfirmAsync(
                Localization.Format("LabTests.DeleteConfirm", item.Name), confirmText: Localization.Get("Common.Delete"), isDanger: true))
        {
            return;
        }

        if (await ReadAsync(ct => _tests.DeleteTestAsync(item.Id, ct)) is not { } result)
        {
            return;
        }

        if (!result.IsSuccess)
        {
            ShowError(result);
            return;
        }

        Toasts.Success(Localization.Get("LabTests.Deleted"));
        await ReloadAsync();
    }

    /// <summary>All tests, whatever the search or page shown, as a PDF opened in the default viewer.</summary>
    [RelayCommand]
    private async Task ExportAsync()
    {
        if (await ReadAsync(ct => _tests.GetAllTestsAsync(ct)) is not { } result)
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
            await _print.PrintAsync(new LabTestsDocument(_print.CreateContext(), result.Value!), "lab-tests");
        }
        catch (Exception ex)
        {
            _errors.Report(ex, context: "Exporting the lab tests");
            Toasts.Error(Localization.Get("Print.Failed"));
        }
    }
}
