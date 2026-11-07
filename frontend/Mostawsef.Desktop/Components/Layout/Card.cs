using Avalonia;
using Avalonia.Controls;

namespace Mostawsef.Desktop.Components;

/// <summary>Surface container. Variants (Classes): default elevated, "outlined", "flat", "interactive".</summary>
public class Card : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Card, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<Card, string?>(nameof(Subtitle));

    public static readonly StyledProperty<object?> HeaderActionsProperty =
        AvaloniaProperty.Register<Card, object?>(nameof(HeaderActions));

    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<Card, object?>(nameof(Footer));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public object? HeaderActions
    {
        get => GetValue(HeaderActionsProperty);
        set => SetValue(HeaderActionsProperty, value);
    }

    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }
}
