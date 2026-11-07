using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace Mostawsef.Desktop.Components;

/// <summary>Content of a toast notification (icon by Kind, optional title, message, close button). Shown by ToastService.</summary>
[PseudoClasses(":success", ":warning", ":danger", ":info")]
public class Toast : TemplatedControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Toast, string?>(nameof(Title));

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<Toast, string?>(nameof(Message));

    public static readonly StyledProperty<BadgeKind> KindProperty =
        AvaloniaProperty.Register<Toast, BadgeKind>(nameof(Kind), BadgeKind.Info);

    public Toast()
    {
        UpdateKind();
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public BadgeKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == KindProperty)
        {
            UpdateKind();
        }
    }

    private void UpdateKind()
    {
        PseudoClasses.Set(":success", Kind == BadgeKind.Success);
        PseudoClasses.Set(":warning", Kind == BadgeKind.Warning);
        PseudoClasses.Set(":danger", Kind == BadgeKind.Danger);
        PseudoClasses.Set(":info", Kind is BadgeKind.Info or BadgeKind.Primary or BadgeKind.Neutral);
    }
}
