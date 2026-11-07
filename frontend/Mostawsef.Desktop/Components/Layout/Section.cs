using Avalonia;
using Avalonia.Controls;

namespace Mostawsef.Desktop.Components;

/// <summary>Titled block of page content without a surface.</summary>
public class Section : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Section, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<Section, string?>(nameof(Description));

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<Section, object?>(nameof(Actions));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }
}
