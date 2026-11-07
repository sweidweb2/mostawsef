using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace Mostawsef.Desktop.Components;

/// <summary>
/// List and details side by side. Below the Medium breakpoint it stacks:
/// the list is shown, or the details when IsDetailsOpen is true.
/// </summary>
[PseudoClasses(":stacked", ":details-open")]
public class ListDetailsView : TemplatedControl
{
    public static readonly StyledProperty<object?> ListProperty =
        AvaloniaProperty.Register<ListDetailsView, object?>(nameof(List));

    public static readonly StyledProperty<object?> DetailsProperty =
        AvaloniaProperty.Register<ListDetailsView, object?>(nameof(Details));

    public static readonly StyledProperty<double> ListWidthProperty =
        AvaloniaProperty.Register<ListDetailsView, double>(nameof(ListWidth), 340);

    public static readonly StyledProperty<bool> IsDetailsOpenProperty =
        AvaloniaProperty.Register<ListDetailsView, bool>(nameof(IsDetailsOpen));

    public object? List
    {
        get => GetValue(ListProperty);
        set => SetValue(ListProperty, value);
    }

    public object? Details
    {
        get => GetValue(DetailsProperty);
        set => SetValue(DetailsProperty, value);
    }

    public double ListWidth
    {
        get => GetValue(ListWidthProperty);
        set => SetValue(ListWidthProperty, value);
    }

    public bool IsDetailsOpen
    {
        get => GetValue(IsDetailsOpenProperty);
        set => SetValue(IsDetailsOpenProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BoundsProperty)
        {
            PseudoClasses.Set(":stacked", Bounds.Width < Breakpoints.Medium);
        }
        else if (change.Property == IsDetailsOpenProperty)
        {
            PseudoClasses.Set(":details-open", IsDetailsOpen);
        }
    }
}
