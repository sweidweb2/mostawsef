using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Mostawsef.Desktop.Services.Ui;

/// <summary>
/// Asks the user to pick a folder: returns the chosen full path, or null when they cancel.
/// Registered in DI so tests can answer without a real dialog.
/// </summary>
public delegate Task<string?> FolderPathPicker(string title);

/// <summary>The real folder dialog of the operating system, shown over the main window.</summary>
public static class FolderPicker
{
    public static async Task<string?> PickAsync(string title)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            return null;
        }

        var storage = window.StorageProvider;
        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
