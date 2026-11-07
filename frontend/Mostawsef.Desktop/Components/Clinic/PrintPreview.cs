using System;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;

namespace Mostawsef.Desktop.Components;

/// <summary>
/// Paper-sized preview of printable content (prescriptions, invoices, reports).
/// The page always renders in the Light theme. Printing itself is supplied through PrintCommand.
/// </summary>
[TemplatePart("PART_Zoom", typeof(LayoutTransformControl))]
public class PrintPreview : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<PrintPreview, string?>(nameof(Title));

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<PrintPreview, double>(nameof(Zoom), 0.8);

    /// <summary>A4 at 96 DPI.</summary>
    public static readonly StyledProperty<double> PaperWidthProperty =
        AvaloniaProperty.Register<PrintPreview, double>(nameof(PaperWidth), 794);

    public static readonly StyledProperty<double> PaperHeightProperty =
        AvaloniaProperty.Register<PrintPreview, double>(nameof(PaperHeight), 1123);

    public static readonly StyledProperty<ICommand?> PrintCommandProperty =
        AvaloniaProperty.Register<PrintPreview, ICommand?>(nameof(PrintCommand));

    public static readonly DirectProperty<PrintPreview, string> ZoomTextProperty =
        AvaloniaProperty.RegisterDirect<PrintPreview, string>(nameof(ZoomText), o => o.ZoomText);

    public static readonly DirectProperty<PrintPreview, ICommand> ZoomInCommandProperty =
        AvaloniaProperty.RegisterDirect<PrintPreview, ICommand>(nameof(ZoomInCommand), o => o.ZoomInCommand);

    public static readonly DirectProperty<PrintPreview, ICommand> ZoomOutCommandProperty =
        AvaloniaProperty.RegisterDirect<PrintPreview, ICommand>(nameof(ZoomOutCommand), o => o.ZoomOutCommand);

    private LayoutTransformControl? _zoomHost;
    private string _zoomText = "80%";

    public PrintPreview()
    {
        ZoomInCommand = new RelayCommand(() => Zoom = Math.Min(2, Math.Round(Zoom + 0.1, 1)));
        ZoomOutCommand = new RelayCommand(() => Zoom = Math.Max(0.3, Math.Round(Zoom - 0.1, 1)));
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public double PaperWidth
    {
        get => GetValue(PaperWidthProperty);
        set => SetValue(PaperWidthProperty, value);
    }

    public double PaperHeight
    {
        get => GetValue(PaperHeightProperty);
        set => SetValue(PaperHeightProperty, value);
    }

    public ICommand? PrintCommand
    {
        get => GetValue(PrintCommandProperty);
        set => SetValue(PrintCommandProperty, value);
    }

    public string ZoomText
    {
        get => _zoomText;
        private set => SetAndRaise(ZoomTextProperty, ref _zoomText, value);
    }

    public ICommand ZoomInCommand { get; }

    public ICommand ZoomOutCommand { get; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _zoomHost = e.NameScope.Find<LayoutTransformControl>("PART_Zoom");
        ApplyZoom();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ZoomProperty)
        {
            ApplyZoom();
        }
    }

    private void ApplyZoom()
    {
        ZoomText = Zoom.ToString("P0", CultureInfo.InvariantCulture).Replace(" ", string.Empty);
        if (_zoomHost is not null)
        {
            _zoomHost.LayoutTransform = new ScaleTransform(Zoom, Zoom);
        }
    }
}
