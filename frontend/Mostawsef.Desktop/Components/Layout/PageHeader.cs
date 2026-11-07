using Avalonia;
using Avalonia.Controls.Primitives;

namespace Mostawsef.Desktop.Components;

public class PageHeader : TemplatedControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<PageHeader, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<PageHeader, string?>(nameof(Subtitle));

    public static readonly StyledProperty<object?> BreadcrumbProperty =
        AvaloniaProperty.Register<PageHeader, object?>(nameof(Breadcrumb));

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<PageHeader, object?>(nameof(Actions));

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

    public object? Breadcrumb
    {
        get => GetValue(BreadcrumbProperty);
        set => SetValue(BreadcrumbProperty, value);
    }

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }
}
