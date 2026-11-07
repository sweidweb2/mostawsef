using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace Mostawsef.Desktop.Components;

public enum BadgeKind
{
    Neutral,
    Primary,
    Success,
    Warning,
    Danger,
    Info,
}

/// <summary>Small status pill: Kind="Success" Text="Paid".</summary>
[PseudoClasses(":primary", ":success", ":warning", ":danger", ":info")]
public class Badge : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<Badge, string?>(nameof(Text));

    public static readonly StyledProperty<BadgeKind> KindProperty =
        AvaloniaProperty.Register<Badge, BadgeKind>(nameof(Kind));

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

        if (change.Property == KindProperty)
        {
            PseudoClasses.Set(":primary", Kind == BadgeKind.Primary);
            PseudoClasses.Set(":success", Kind == BadgeKind.Success);
            PseudoClasses.Set(":warning", Kind == BadgeKind.Warning);
            PseudoClasses.Set(":danger", Kind == BadgeKind.Danger);
            PseudoClasses.Set(":info", Kind == BadgeKind.Info);
        }
    }
}
