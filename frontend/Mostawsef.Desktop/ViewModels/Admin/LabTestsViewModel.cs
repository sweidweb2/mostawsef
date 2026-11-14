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
    private readonly LabTestCatalogVersion _catalogVersion;
    private readonly IDialogService _dialogs;
    private readonly PrintService _print;
    private readonly SavePathPicker _pickSavePath;
    private readonly ErrorReporter _errors;

    public LabTestsViewModel(
        LabTestsApi tests,
        LabTestCatalogVersion catalogVersion,
        IDialogService dialogs,
        IToastService toasts,
        LocalizationService localization,
        PrintService print,
        SavePathPicker pickSavePath,
        ErrorReporter errors)
        : base(toasts, localization, errors)
    {
        _tests = tests;
        _catalogVersion = catalogVersion;
        _dialogs = dialogs;
        _print = print;
        _pickSavePath = pickSavePath;
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
        var form = new LabTestFormViewModel(_tests, _dialogs, Localization);
        if (!await _dialogs.ShowFormAsync(form, Localization.Get("LabTests.AddTitle")))
        {
            return;
        }

        Toasts.Success(Localization.Get("LabTests.Saved"));
        _catalogVersion.Invalidate();
        if (form.SavedValue is LabTestDto created)
        {
            InsertSorted(created, CompareByName);
        }
        else
        {
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
            _catalogVersion.Invalidate();
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
        _catalogVersion.Invalidate();
        await ReloadAsync();
    }

    /// <summary>True while the export is fetching and writing the PDF (its button shows a spinner).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    public partial bool IsExporting { get; private set; }

    private bool CanExport() => !IsExporting;

    /// <summary>Asks where to save, then writes up to <see cref="PageQuery.MaxExportRows"/> tests there as a PDF.</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        string? path;
        try
        {
            path = await _pickSavePath("lab-tests.pdf", Localization.Get("Print.SaveTitle"));
        }
        catch (Exception ex)
        {
            _errors.Report(ex, context: "Choosing where to save the tests export");
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
            if (await ReadAsync(ct => _tests.GetAllTestsAsync(ct)) is not { } result)
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

            await _print.SaveAsync(new LabTestsDocument(await _print.CreateContextAsync(), rows), path);
            Toasts.Success(Localization.Format("Print.Exported", path));
        }
        catch (Exception ex)
        {
            _errors.Report(ex, context: "Exporting the lab tests");
            Toasts.Error(Localization.Get("Print.Failed"));
        }
        finally
        {
            IsExporting = false;
        }
    }

    private static int CompareByName(LabTestDto a, LabTestDto b)
    {
        var byName = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        return byName != 0 ? byName : a.Id.CompareTo(b.Id);
    }
}
