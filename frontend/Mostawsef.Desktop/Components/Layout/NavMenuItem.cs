using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Mostawsef.Desktop.Components;

public class NavMenuItem : ListBoxItem
{
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<NavMenuItem, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<NavMenuItem, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<NavMenuItem, bool>(nameof(IsCompact));

    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsCompactProperty || change.Property == TextProperty)
        {
            ToolTip.SetTip(this, IsCompact ? Text : null);
        }
    }
}
