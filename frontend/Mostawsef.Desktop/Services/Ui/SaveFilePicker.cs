using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Mostawsef.Desktop.Services.Ui;

/// <summary>
/// Asks the user where to save a file: returns the chosen full path, or null when they cancel.
/// Registered in DI so tests can answer without a real dialog.
/// </summary>
public delegate Task<string?> SavePathPicker(string suggestedFileName, string title);

/// <summary>The real "Save as" dialog of the operating system, shown over the main window and starting in Downloads.</summary>
public static class SaveFilePicker
{
    /// <summary>The user's Downloads folder (where the dialog opens).</summary>
    public static string DownloadsFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    /// <summary>Asks where to save <paramref name="suggestedFileName"/>; the type filter follows its extension (.pdf, .csv, .json, .sql, .zip).</summary>
    public static async Task<string?> PickAsync(string suggestedFileName, string title)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            return null;
        }

        var storage = window.StorageProvider;
        var extension = Path.GetExtension(suggestedFileName).TrimStart('.').ToLowerInvariant();
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = extension,
            ShowOverwritePrompt = true,
            FileTypeChoices = [FileTypeFor(extension)],
            SuggestedStartLocation = Directory.Exists(DownloadsFolder) ? await storage.TryGetFolderFromPathAsync(DownloadsFolder) : null,
        });

        return file?.TryGetLocalPath();
    }

    private static FilePickerFileType FileTypeFor(string extension) => extension switch
    {
        "pdf" => FilePickerFileTypes.Pdf,
        "csv" => new FilePickerFileType("CSV") { Patterns = ["*.csv"], MimeTypes = ["text/csv"] },
        "json" => new FilePickerFileType("JSON") { Patterns = ["*.json"], MimeTypes = ["application/json"] },
        "zip" => new FilePickerFileType("ZIP") { Patterns = ["*.zip"], MimeTypes = ["application/zip"] },
        "sql" => new FilePickerFileType("SQL") { Patterns = ["*.sql"], MimeTypes = ["application/sql"] },
        _ => FilePickerFileTypes.All,
    };
}
