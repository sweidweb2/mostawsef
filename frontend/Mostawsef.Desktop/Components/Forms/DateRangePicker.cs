using System;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace Mostawsef.Desktop.Components;

/// <summary>Two date pickers (From / To). To is never earlier than From.</summary>
public class DateRangePicker : TemplatedControl
{
    public static readonly StyledProperty<DateTime?> FromProperty =
        AvaloniaProperty.Register<DateRangePicker, DateTime?>(nameof(From), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<DateTime?> ToProperty =
        AvaloniaProperty.Register<DateRangePicker, DateTime?>(nameof(To), defaultBindingMode: BindingMode.TwoWay);

    public DateTime? From
    {
        get => GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    public DateTime? To
    {
        get => GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if ((change.Property == FromProperty || change.Property == ToProperty)
            && From is { } from && To is { } to && to < from)
        {
            if (change.Property == FromProperty)
            {
                To = from;
            }
            else
            {
                From = to;
            }
        }
    }
}
