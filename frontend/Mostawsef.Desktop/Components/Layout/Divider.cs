using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace Mostawsef.Desktop.Components;

/// <summary>Horizontal or vertical rule, optionally with a centered label.</summary>
public class Divider : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<Divider, string?>(nameof(Text));

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<Divider, Orientation>(nameof(Orientation));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }
}
