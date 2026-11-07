using System;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;

namespace Mostawsef.Desktop.Components;

/// <summary>FileUpload restricted to images, with a preview.</summary>
public class ImagePicker : FileUpload
{
    public static readonly DirectProperty<ImagePicker, Bitmap?> PreviewProperty =
        AvaloniaProperty.RegisterDirect<ImagePicker, Bitmap?>(nameof(Preview), o => o.Preview);

    private Bitmap? _preview;

    public ImagePicker()
    {
        FileTypes = "*.png;*.jpg;*.jpeg;*.bmp;*.webp";
    }

    public Bitmap? Preview
    {
        get => _preview;
        private set => SetAndRaise(PreviewProperty, ref _preview, value);
    }

    protected override string DialogTitleKey => "ImagePicker.Title";

    protected override void OnFileChanged()
    {
        var old = Preview;
        Preview = null;
        old?.Dispose();

        if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath))
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(FilePath);
            Preview = Bitmap.DecodeToWidth(stream, 400);
        }
        catch (Exception)
        {
            Preview = null;
        }
    }
}
