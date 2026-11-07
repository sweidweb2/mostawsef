using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace Mostawsef.Desktop.Components;

/// <summary>Lists form errors. Hidden automatically when Errors is empty.</summary>
public class ValidationSummary : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable?> ErrorsProperty =
        AvaloniaProperty.Register<ValidationSummary, IEnumerable?>(nameof(Errors));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ValidationSummary, string?>(nameof(Title));

    public IEnumerable? Errors
    {
        get => GetValue(ErrorsProperty);
        set => SetValue(ErrorsProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ErrorsProperty)
        {
            if (change.OldValue is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= OnErrorsChanged;
            }

            if (change.NewValue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += OnErrorsChanged;
            }

            UpdateVisibility();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateVisibility();
    }

    private void OnErrorsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateVisibility();

    private void UpdateVisibility() => IsVisible = Errors?.Cast<object>().Any() == true;
}
