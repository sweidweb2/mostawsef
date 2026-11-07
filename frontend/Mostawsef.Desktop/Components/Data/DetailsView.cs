using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Mostawsef.Desktop.Components;

/// <summary>Vertical list of label / value rows (DetailsItem children), e.g. inside a details modal.</summary>
public class DetailsView : ItemsControl
{
}

/// <summary>One label / value row. Empty values show a dash. IsCode shows the value in a scrollable monospace box.</summary>
public class DetailsItem : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<DetailsItem, string?>(nameof(Label));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<DetailsItem, string?>(nameof(Value));

    public static readonly StyledProperty<bool> IsCodeProperty =
        AvaloniaProperty.Register<DetailsItem, bool>(nameof(IsCode));

    public static readonly DirectProperty<DetailsItem, string> DisplayValueProperty =
        AvaloniaProperty.RegisterDirect<DetailsItem, string>(nameof(DisplayValue), o => o.DisplayValue);

    private string _displayValue = "—";

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool IsCode
    {
        get => GetValue(IsCodeProperty);
        set => SetValue(IsCodeProperty, value);
    }

    public string DisplayValue
    {
        get => _displayValue;
        private set => SetAndRaise(DisplayValueProperty, ref _displayValue, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty)
        {
            DisplayValue = string.IsNullOrWhiteSpace(Value) ? "—" : Value;
        }
    }
}
