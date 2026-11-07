using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace Mostawsef.Desktop.Components;

/// <summary>Numbered progress indicator for multi-step flows.</summary>
public class Stepper : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<string>?> StepsProperty =
        AvaloniaProperty.Register<Stepper, IReadOnlyList<string>?>(nameof(Steps));

    public static readonly StyledProperty<int> CurrentIndexProperty =
        AvaloniaProperty.Register<Stepper, int>(nameof(CurrentIndex));

    public static readonly DirectProperty<Stepper, IReadOnlyList<StepperStep>> ItemsProperty =
        AvaloniaProperty.RegisterDirect<Stepper, IReadOnlyList<StepperStep>>(nameof(Items), o => o.Items);

    private IReadOnlyList<StepperStep> _items = [];

    public IReadOnlyList<string>? Steps
    {
        get => GetValue(StepsProperty);
        set => SetValue(StepsProperty, value);
    }

    public int CurrentIndex
    {
        get => GetValue(CurrentIndexProperty);
        set => SetValue(CurrentIndexProperty, value);
    }

    public IReadOnlyList<StepperStep> Items
    {
        get => _items;
        private set => SetAndRaise(ItemsProperty, ref _items, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == StepsProperty || change.Property == CurrentIndexProperty)
        {
            var steps = Steps ?? [];
            Items = steps
                .Select((title, i) => new StepperStep(
                    Number: (i + 1).ToString(),
                    Title: title,
                    IsDone: i < CurrentIndex,
                    IsCurrent: i == CurrentIndex,
                    IsLast: i == steps.Count - 1))
                .ToList();
        }
    }
}

public record StepperStep(string Number, string Title, bool IsDone, bool IsCurrent, bool IsLast);
