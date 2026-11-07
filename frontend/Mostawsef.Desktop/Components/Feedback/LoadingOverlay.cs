using Avalonia;
using Avalonia.Controls;

namespace Mostawsef.Desktop.Components;

/// <summary>Covers its Content with a spinner (and optional Message) while IsLoading is true.</summary>
public class LoadingOverlay : ContentControl
{
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<LoadingOverlay, bool>(nameof(IsLoading));

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<LoadingOverlay, string?>(nameof(Message));

    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }
}
