using Avalonia;
using Avalonia.Controls;

namespace Mostawsef.Desktop.Components;

/// <summary>Sidebar navigation list. Selection marks the active page.</summary>
public class NavMenu : ListBox
{
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<NavMenu, bool>(nameof(IsCompact));

    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<NavMenuItem>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new NavMenuItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);

        if (container is NavMenuItem navItem)
        {
            navItem.IsCompact = IsCompact;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsCompactProperty)
        {
            foreach (var container in GetRealizedContainers())
            {
                if (container is NavMenuItem navItem)
                {
                    navItem.IsCompact = IsCompact;
                }
            }
        }
    }
}
