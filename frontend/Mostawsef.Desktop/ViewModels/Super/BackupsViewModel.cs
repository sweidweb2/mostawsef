using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Backups;
using Mostawsef.Contracts.Common;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Super;

/// <summary>One format of the new-backup form: <paramref name="Format"/> is the API name, the texts are "BackupFormat.&lt;Format&gt;" and ".Hint".</summary>
public sealed record BackupFormatOption(string Format);

/// <summary>
/// Super portal: export the database (every table or one) as CSV, JSON, SQL or PostgreSQL, and the history of past exports.
/// Each export saves only its settings; exporting a history row again uses them on the current data.
/// Asking where to save always comes first, so cancelling the dialog stores nothing.
/// </summary>
public partial class BackupsViewModel : NumberedListViewModel<BackupRecordDto>
{
    public static IReadOnlyList<BackupFormatOption> FormatOptions { get; } =
        BackupFormats.All.Select(format => new BackupFormatOption(format)).ToList();

    private readonly BackupsApi _backups;
    private readonly SavePathPicker _pickSavePath;

    public BackupsViewModel(
        BackupsApi backups,
        SavePathPicker pickSavePath,
        IToastService toasts,
        LocalizationService localization,
        ErrorReporter errors)
        : base(toasts, localization, errors)
    {
        _backups = backups;
        _pickSavePath = pickSavePath;
        SelectedFormat = FormatOptions[0];
    }

    /// <summary>The tables the form offers; filled when the page opens.</summary>
    public ObservableCollection<string> Tables { get; } = [];

    /// <summary>True: every table. False: only <see cref="SelectedTable"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOneTable))]
    [NotifyCanExecuteChangedFor(nameof(ExportNewCommand))]
    public partial bool IsAllTables { get; set; } = true;

    public bool IsOneTable
    {
        get => !IsAllTables;
        set => IsAllTables = !value;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportNewCommand))]
    public partial string? SelectedTable { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedFormatHintKey))]
    public partial BackupFormatOption SelectedFormat { get; set; }

    /// <summary>Resource key of the one-line explanation under the format list.</summary>
    public string SelectedFormatHintKey => $"BackupFormat.{SelectedFormat.Format}.Hint";

    /// <summary>True while a backup is being saved and written (every export button waits).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportNewCommand), nameof(ExportCommand))]
    public partial bool IsExporting { get; private set; }

    protected override async Task LoadOnActivateAsync()
    {
        await Task.WhenAll(ReloadAsync(), LoadTablesAsync());
    }

    protected override Task<ApiResult<PagedResult<BackupRecordDto>>> FetchPageAsync(
        int page, int pageSize, string? search, bool includeTotal, CancellationToken cancellationToken) =>
        _backups.GetBackupsAsync(page, pageSize, includeTotal, cancellationToken);

    private async Task LoadTablesAsync()
    {
        if (await ReadAsync(ct => _backups.GetTablesAsync(ct)) is not { IsSuccess: true } result)
        {
            return;
        }

        Tables.Clear();
        foreach (var table in result.Value!)
        {
            Tables.Add(table);
        }

        if (SelectedTable is null || !Tables.Contains(SelectedTable))
        {
            SelectedTable = Tables.FirstOrDefault();
        }
    }

    private bool CanExportNew() => !IsExporting && (IsAllTables || SelectedTable is not null);

    /// <summary>Asks where to save, saves the settings as a history row, then writes the file there.</summary>
    [RelayCommand(CanExecute = nameof(CanExportNew))]
    private async Task ExportNewAsync()
    {
        var table = IsAllTables ? null : SelectedTable;
        var format = SelectedFormat.Format;
        if (await AskWhereAsync(table, format) is not { } path)
        {
            return;
        }

        IsExporting = true;
        try
        {
            if (await ReadAsync(ct => _backups.CreateAsync(new CreateBackupRequest { Table = table, Format = format }, ct)) is not { } created)
            {
                return;
            }

            if (!created.IsSuccess)
            {
                ShowError(created);
                return;
            }

            PrependNewest(created.Value!);
            await WriteAsync(created.Value!, path);
        }
        catch (Exception ex)
        {
            Errors.Report(ex, context: "Exporting a backup");
            Toasts.Error(Localization.Get("Backups.Failed"));
        }
        finally
        {
            IsExporting = false;
        }
    }

    private bool CanExport(BackupRecordDto? item) => !IsExporting && item is not null;

    /// <summary>Asks where to save, then exports the current data with the settings of a history row.</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync(BackupRecordDto? item)
    {
        if (item is null || await AskWhereAsync(item.Table, item.Format) is not { } path)
        {
            return;
        }

        IsExporting = true;
        try
        {
            await WriteAsync(item, path);
        }
        catch (Exception ex)
        {
            Errors.Report(ex, context: "Exporting a backup again");
            Toasts.Error(Localization.Get("Backups.Failed"));
        }
        finally
        {
            IsExporting = false;
        }
    }

    /// <summary>The "Save as" dialog, suggesting a name built from the settings. Null when the user cancels (or the dialog fails).</summary>
    private async Task<string?> AskWhereAsync(string? table, string format)
    {
        try
        {
            return await _pickSavePath(
                BackupFormats.FileName(table, format, DateTime.Now),
                Localization.Get("Backups.SaveTitle"));
        }
        catch (Exception ex)
        {
            Errors.Report(ex, context: "Choosing where to save the backup");
            Toasts.Error(Localization.Get("Backups.Failed"));
            return null;
        }
    }

    private async Task WriteAsync(BackupRecordDto record, string path)
    {
        if (await ReadAsync(ct => _backups.ExportAsync(record.Id, path, ct)) is not { } result)
        {
            return;
        }

        if (!result.IsSuccess)
        {
            ShowError(result);
            return;
        }

        Toasts.Success(Localization.Format("Backups.Exported", path));
    }
}
