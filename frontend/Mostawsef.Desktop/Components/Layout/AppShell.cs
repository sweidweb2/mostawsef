using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using CommunityToolkit.Mvvm.Input;

namespace Mostawsef.Desktop.Components;

/// <summary>
/// Application frame: sidebar (header, navigation, footer), top bar and content area.
/// The sidebar collapses to icons automatically when the shell is narrower than AutoCompactWidth.
/// <c>:narrow</c> is set while the content area is narrower than <see cref="NarrowContentWidth"/>; the responsive
/// rules in Styles/Responsive.axaml hang off it (<c>ui|AppShell:narrow .page</c>). It replaces Avalonia container
/// queries, which made adding or removing any control inside the content area 20-60x slower.
/// </summary>
[PseudoClasses(":compact", ":narrow")]
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

    /// <summary>Content width below which <c>:narrow</c> applies (BreakpointMedium in Styles/Tokens/Breakpoints.axaml).</summary>
    public const double NarrowContentWidth = 900;

    private bool? _wasNarrow;
    private Control? _content;

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

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_content is not null)
        {
            _content.PropertyChanged -= OnContentPropertyChanged;
        }

        _content = e.NameScope.Find<Control>("PART_Content");
        if (_content is not null)
        {
            _content.PropertyChanged += OnContentPropertyChanged;
        }
    }

    private void OnContentPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty && _content is { } content)
        {
            // Only flips at the breakpoint, so resizing does not restyle anything in between.
            var narrow = content.Bounds.Width < NarrowContentWidth;
            if (PseudoClasses.Contains(":narrow") != narrow)
            {
                PseudoClasses.Set(":narrow", narrow);
            }
        }
    }

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
