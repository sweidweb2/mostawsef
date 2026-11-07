using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Mostawsef.Desktop.Components;

/// <summary>Square icon-only button. Combine with variant classes: Classes="ghost", "primary", "sm"...</summary>
public class IconButton : Button
{
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<IconButton, Geometry?>(nameof(Icon));

    private readonly PathIcon _icon = new();

    public IconButton()
    {
        Classes.Add("icon");
        Content = _icon;
    }

    protected override Type StyleKeyOverride => typeof(Button);

    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconProperty)
        {
            _icon.Data = Icon;
        }
    }
}
