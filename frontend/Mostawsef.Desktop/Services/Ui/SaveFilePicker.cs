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

    public static async Task<string?> PickPdfAsync(string suggestedFileName, string title)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            return null;
        }

        var storage = window.StorageProvider;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "pdf",
            ShowOverwritePrompt = true,
            FileTypeChoices = [FilePickerFileTypes.Pdf],
            SuggestedStartLocation = Directory.Exists(DownloadsFolder) ? await storage.TryGetFolderFromPathAsync(DownloadsFolder) : null,
        });

        return file?.TryGetLocalPath();
    }
}
