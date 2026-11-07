using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace Mostawsef.Desktop.Components;

/// <summary>Inline message box with an icon: Kind="Danger" Text="..." (Info, Success, Warning, Danger). Hidden while Text is empty.</summary>
[PseudoClasses(":success", ":warning", ":danger")]
public class Alert : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<Alert, string?>(nameof(Text));

    public static readonly StyledProperty<BadgeKind> KindProperty =
        AvaloniaProperty.Register<Alert, BadgeKind>(nameof(Kind), BadgeKind.Info);

    static Alert()
    {
        IsVisibleProperty.OverrideDefaultValue<Alert>(false);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public BadgeKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            SetCurrentValue(IsVisibleProperty, !string.IsNullOrEmpty(Text));
        }
        else if (change.Property == KindProperty)
        {
            PseudoClasses.Set(":success", Kind == BadgeKind.Success);
            PseudoClasses.Set(":warning", Kind == BadgeKind.Warning);
            PseudoClasses.Set(":danger", Kind == BadgeKind.Danger);
        }
    }
}
