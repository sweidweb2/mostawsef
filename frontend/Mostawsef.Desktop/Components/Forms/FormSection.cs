using Avalonia;
using Avalonia.Controls;

namespace Mostawsef.Desktop.Components;

/// <summary>Titled group of FormFields laid out in a ResponsiveGrid (1..MaxColumns columns).</summary>
public class FormSection : ItemsControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<FormSection, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<FormSection, string?>(nameof(Description));

    public static readonly StyledProperty<int> MaxColumnsProperty =
        AvaloniaProperty.Register<FormSection, int>(nameof(MaxColumns), 2);

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

    public int MaxColumns
    {
        get => GetValue(MaxColumnsProperty);
        set => SetValue(MaxColumnsProperty, value);
    }
}
