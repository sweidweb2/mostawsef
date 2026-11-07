using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;

namespace Mostawsef.Desktop.Components;

/// <summary>Pick a single local file. FilePath holds the selected path.</summary>
[PseudoClasses(":has-file")]
public class FileUpload : TemplatedControl
{
    public static readonly StyledProperty<string?> FilePathProperty =
        AvaloniaProperty.Register<FileUpload, string?>(nameof(FilePath), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<FileUpload, string?>(nameof(Title));

    /// <summary>Allowed extensions, e.g. "*.pdf;*.jpg". Empty means any file.</summary>
    public static readonly StyledProperty<string?> FileTypesProperty =
        AvaloniaProperty.Register<FileUpload, string?>(nameof(FileTypes));

    public static readonly DirectProperty<FileUpload, string?> FileNameProperty =
        AvaloniaProperty.RegisterDirect<FileUpload, string?>(nameof(FileName), o => o.FileName);

    public static readonly DirectProperty<FileUpload, ICommand> BrowseCommandProperty =
        AvaloniaProperty.RegisterDirect<FileUpload, ICommand>(nameof(BrowseCommand), o => o.BrowseCommand);

    public static readonly DirectProperty<FileUpload, ICommand> ClearCommandProperty =
        AvaloniaProperty.RegisterDirect<FileUpload, ICommand>(nameof(ClearCommand), o => o.ClearCommand);

    private string? _fileName;

    public FileUpload()
    {
        BrowseCommand = new AsyncRelayCommand(BrowseAsync);
        ClearCommand = new RelayCommand(() => FilePath = null);
    }

    public string? FilePath
    {
        get => GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? FileTypes
    {
        get => GetValue(FileTypesProperty);
        set => SetValue(FileTypesProperty, value);
    }

    public string? FileName
    {
        get => _fileName;
        private set => SetAndRaise(FileNameProperty, ref _fileName, value);
    }

    public ICommand BrowseCommand { get; }

    public ICommand ClearCommand { get; }

    protected virtual string DialogTitleKey => "FileUpload.Title";

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == FilePathProperty)
        {
            FileName = string.IsNullOrEmpty(FilePath) ? null : Path.GetFileName(FilePath);
            PseudoClasses.Set(":has-file", !string.IsNullOrEmpty(FilePath));
            OnFileChanged();
        }
    }

    protected virtual void OnFileChanged()
    {
    }

    private async Task BrowseAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        var options = new FilePickerOpenOptions
        {
            Title = Title ?? App.Localization.Get(DialogTitleKey),
            AllowMultiple = false,
        };

        var patterns = ParsePatterns(FileTypes);
        if (patterns.Count > 0)
        {
            options.FileTypeFilter = [new FilePickerFileType(string.Join(", ", patterns)) { Patterns = patterns }];
        }

        var files = await storage.OpenFilePickerAsync(options);
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
        {
            FilePath = path;
        }
    }

    private static List<string> ParsePatterns(string? fileTypes) =>
        string.IsNullOrWhiteSpace(fileTypes)
            ? []
            : fileTypes.Split(';', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries).ToList();
}
