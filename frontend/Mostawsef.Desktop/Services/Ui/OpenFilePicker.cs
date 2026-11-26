using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Mostawsef.Desktop.Services.Ui;

/// <summary>
/// Asks the user to pick an existing file: returns the chosen full path, or null when they cancel.
/// Registered in DI so tests can answer without a real dialog.
/// </summary>
public delegate Task<string?> OpenPathPicker(string title, IReadOnlyList<string> extensions);

/// <summary>The real "Open file" dialog of the operating system, shown over the main window.</summary>
public static class OpenFilePicker
{
    public static async Task<string?> PickAsync(string title, IReadOnlyList<string> extensions)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            return null;
        }

        var storage = window.StorageProvider;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [FileTypeFor(extensions)],
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private static FilePickerFileType FileTypeFor(IReadOnlyList<string> extensions)
    {
        var patterns = new List<string>(extensions.Count);
        foreach (var extension in extensions)
        {
            var clean = extension.Trim().TrimStart('.').ToLowerInvariant();
            if (clean.Length > 0)
            {
                patterns.Add($"*.{clean}");
            }
        }

        return new FilePickerFileType("Import")
        {
            Patterns = patterns.Count > 0 ? patterns : ["*.*"],
        };
    }
}
