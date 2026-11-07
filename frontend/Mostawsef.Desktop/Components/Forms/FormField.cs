using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace Mostawsef.Desktop.Components;

/// <summary>Label + input (Content) + hint or error message.</summary>
[PseudoClasses(":error")]
public class FormField : ContentControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<FormField, string?>(nameof(Label));

    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<FormField, string?>(nameof(Hint));

    public static readonly StyledProperty<string?> ErrorProperty =
        AvaloniaProperty.Register<FormField, string?>(nameof(Error));

    public static readonly StyledProperty<bool> IsRequiredProperty =
        AvaloniaProperty.Register<FormField, bool>(nameof(IsRequired));

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    public string? Error
    {
        get => GetValue(ErrorProperty);
        set => SetValue(ErrorProperty, value);
    }

    public bool IsRequired
    {
        get => GetValue(IsRequiredProperty);
        set => SetValue(IsRequiredProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ErrorProperty)
        {
            PseudoClasses.Set(":error", !string.IsNullOrEmpty(Error));
        }
    }
}
