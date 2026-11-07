using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using CommunityToolkit.Mvvm.Input;

namespace Mostawsef.Desktop.Components;

/// <summary>
/// Application frame: sidebar (header, navigation, footer), top bar and content area.
/// The sidebar collapses to icons automatically when the shell is narrower than AutoCompactWidth.
/// The content area is the "AppContent" container used by responsive ContainerQuery rules.
/// </summary>
[PseudoClasses(":compact")]
public class AppShell : ContentControl
{
    public static readonly StyledProperty<object?> SidebarHeaderProperty =
        AvaloniaProperty.Register<AppShell, object?>(nameof(SidebarHeader));

    public static readonly StyledProperty<object?> SidebarProperty =
        AvaloniaProperty.Register<AppShell, object?>(nameof(Sidebar));

    public static readonly StyledProperty<object?> SidebarFooterProperty =
        AvaloniaProperty.Register<AppShell, object?>(nameof(SidebarFooter));

    public static readonly StyledProperty<object?> TopBarProperty =
        AvaloniaProperty.Register<AppShell, object?>(nameof(TopBar));

    public static readonly StyledProperty<bool> IsSidebarCompactProperty =
        AvaloniaProperty.Register<AppShell, bool>(nameof(IsSidebarCompact));

    public static readonly StyledProperty<double> AutoCompactWidthProperty =
        AvaloniaProperty.Register<AppShell, double>(nameof(AutoCompactWidth), 1100);

    public static readonly DirectProperty<AppShell, ICommand> ToggleSidebarCommandProperty =
        AvaloniaProperty.RegisterDirect<AppShell, ICommand>(nameof(ToggleSidebarCommand), o => o.ToggleSidebarCommand);

    private bool? _wasNarrow;

    public AppShell()
    {
        ToggleSidebarCommand = new RelayCommand(() => IsSidebarCompact = !IsSidebarCompact);
    }

    public object? SidebarHeader
    {
        get => GetValue(SidebarHeaderProperty);
        set => SetValue(SidebarHeaderProperty, value);
    }

    public object? Sidebar
    {
        get => GetValue(SidebarProperty);
        set => SetValue(SidebarProperty, value);
    }

    public object? SidebarFooter
    {
        get => GetValue(SidebarFooterProperty);
        set => SetValue(SidebarFooterProperty, value);
    }

    public object? TopBar
    {
        get => GetValue(TopBarProperty);
        set => SetValue(TopBarProperty, value);
    }

    public bool IsSidebarCompact
    {
        get => GetValue(IsSidebarCompactProperty);
        set => SetValue(IsSidebarCompactProperty, value);
    }

    /// <summary>Shell width below which the sidebar collapses automatically. 0 disables auto collapse.</summary>
    public double AutoCompactWidth
    {
        get => GetValue(AutoCompactWidthProperty);
        set => SetValue(AutoCompactWidthProperty, value);
    }

    public ICommand ToggleSidebarCommand { get; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsSidebarCompactProperty)
        {
            PseudoClasses.Set(":compact", IsSidebarCompact);
        }
        else if (change.Property == BoundsProperty && AutoCompactWidth > 0)
        {
            var isNarrow = Bounds.Width < AutoCompactWidth;
            if (isNarrow != _wasNarrow)
            {
                _wasNarrow = isNarrow;
                IsSidebarCompact = isNarrow;
            }
        }
    }
}
