using Avalonia;
using Avalonia.Controls;

namespace Mostawsef.Desktop.Components;

/// <summary>Top application bar: title at the start, Content in the middle, Actions at the end.</summary>
public class TopBar : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<TopBar, string?>(nameof(Title));

    public static readonly StyledProperty<object?> LeadingProperty =
        AvaloniaProperty.Register<TopBar, object?>(nameof(Leading));

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<TopBar, object?>(nameof(Actions));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? Leading
    {
        get => GetValue(LeadingProperty);
        set => SetValue(LeadingProperty, value);
    }

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }
}
