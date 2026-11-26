using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
/// CSV import (with template download), and export of the whole catalog as a PDF.
/// </summary>
public partial class LabTestsViewModel : NumberedListViewModel<LabTestDto>
{
    public const int TestsPerPage = 40;

    private static readonly IReadOnlyList<string> ImportExtensions = ["csv"];

    private readonly LabTestsApi _tests;
    private readonly LabTestCatalogVersion _catalogVersion;
    private readonly IDialogService _dialogs;
    private readonly PrintService _print;
    private readonly SavePathPicker _pickSavePath;
    private readonly OpenPathPicker _pickOpenPath;
    private readonly ErrorReporter _errors;

    public LabTestsViewModel(
        LabTestsApi tests,
        LabTestCatalogVersion catalogVersion,
        IDialogService dialogs,
        IToastService toasts,
        LocalizationService localization,
        PrintService print,
        SavePathPicker pickSavePath,
        OpenPathPicker pickOpenPath,
        ErrorReporter errors)
        : base(toasts, localization, errors)
    {
        _tests = tests;
        _catalogVersion = catalogVersion;
        _dialogs = dialogs;
        _print = print;
        _pickSavePath = pickSavePath;
        _pickOpenPath = pickOpenPath;
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

        var form = new LabTestFormViewModel(_tests, _dialogs, Localization, item);
        if (await _dialogs.ShowFormAsync(form, Localization.Get("LabTests.EditTitle")))
        {
            Toasts.Success(Localization.Get("LabTests.Saved"));
            _catalogVersion.Invalidate();
            if (form.SavedValue is LabTestDto saved)
            {
                ReplaceSorted(saved, t => t.Id, CompareByName);
            }
            else
            {
                await ReloadAsync(includeTotal: false);
            }
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
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadTemplateCommand))]
    public partial bool IsExporting { get; private set; }

    private bool CanExport() => !IsExporting && !IsImporting;

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

    /// <summary>True while an import file is uploading (its button shows a spinner).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadTemplateCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    public partial bool IsImporting { get; private set; }

    private bool CanImport() => !IsImporting && !IsExporting;

    private bool CanDownloadTemplate() => !IsImporting && !IsExporting;

    /// <summary>Picks a CSV Name+Price file, uploads it, reloads the list, and toasts the counts.</summary>
    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task ImportAsync()
    {
        string? path;
        try
        {
            path = await _pickOpenPath(Localization.Get("LabTests.ImportTitle"), ImportExtensions);
        }
        catch (Exception ex)
        {
            _errors.Report(ex, context: "Choosing a lab tests import file");
            Toasts.Error(Localization.Get("LabTests.ImportFailed"));
            return;
        }

        if (path is null)
        {
            return;
        }

        // Avoid reading a huge file into memory for multipart before the API can reject it.
        try
        {
            if (new FileInfo(path).Length > LabTestRules.MaxImportBytes)
            {
                Toasts.Error(Localization.Get("ErrorCode.too_large"));
                return;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _errors.Report(ex, context: "Checking the lab tests import file size");
            Toasts.Error(Localization.Get("LabTests.ImportFailed"));
            return;
        }

        IsImporting = true;
        try
        {
            if (await ReadAsync(ct => _tests.ImportTestsAsync(path, ct)) is not { } result)
            {
                return;
            }

            if (!result.IsSuccess)
            {
                ShowError(result);
                return;
            }

            var imported = result.Value!;
            _catalogVersion.Invalidate();
            await ReloadAsync();

            if (imported.Imported > 0)
            {
                Toasts.Success(Localization.Format("LabTests.ImportDone", imported.Imported, imported.Skipped));
            }
            else if (imported.Skipped > 0)
            {
                Toasts.Show(Localization.Format("LabTests.ImportNone", imported.Skipped), kind: ToastKind.Warning);
            }
            else
            {
                Toasts.Show(Localization.Get("LabTests.ImportEmpty"), kind: ToastKind.Warning);
            }
        }
        catch (Exception ex)
        {
            _errors.Report(ex, context: "Importing lab tests");
            Toasts.Error(Localization.Get("LabTests.ImportFailed"));
        }
        finally
        {
            IsImporting = false;
        }
    }

    /// <summary>Writes the CSV import template (Excel opens it) to a path the user chooses — no API call.</summary>
    [RelayCommand(CanExecute = nameof(CanDownloadTemplate))]
    private async Task DownloadTemplateAsync()
    {
        string? path;
        try
        {
            path = await _pickSavePath(LabTestImportCsv.FileName, Localization.Get("LabTests.TemplateSaveTitle"));
        }
        catch (Exception ex)
        {
            _errors.Report(ex, context: "Choosing where to save the lab tests import template");
            Toasts.Error(Localization.Get("LabTests.ImportFailed"));
            return;
        }

        if (path is null)
        {
            return;
        }

        IsImporting = true;
        try
        {
            var bom = Encoding.UTF8.GetPreamble();
            var body = Encoding.UTF8.GetBytes(LabTestImportCsv.Content);
            var bytes = new byte[bom.Length + body.Length];
            bom.CopyTo(bytes, 0);
            body.CopyTo(bytes, bom.Length);
            await File.WriteAllBytesAsync(path, bytes);
            Toasts.Success(Localization.Format("LabTests.TemplateSaved", path));
        }
        catch (Exception ex)
        {
            _errors.Report(ex, context: "Writing the lab tests import template");
            Toasts.Error(Localization.Get("LabTests.ImportFailed"));
        }
        finally
        {
            IsImporting = false;
        }
    }

    private static int CompareByName(LabTestDto a, LabTestDto b)
    {
        var byName = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        return byName != 0 ? byName : a.Id.CompareTo(b.Id);
    }
}
