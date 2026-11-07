using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Mostawsef.Desktop.Components;

/// <summary>
/// Sidebar entry. Inside a NavMenu it selects a page; standalone (e.g. a sidebar footer "Sign out")
/// it runs Command on click/tap or Enter/Space.
/// </summary>
public class NavMenuItem : ListBoxItem
{
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<NavMenuItem, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<NavMenuItem, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<NavMenuItem, bool>(nameof(IsCompact));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<NavMenuItem, ICommand?>(nameof(Command));

    public NavMenuItem()
    {
        Tapped += (_, _) => ExecuteCommand();
    }

    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Command is not null && e.Key is Key.Enter or Key.Space)
        {
            ExecuteCommand();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsCompactProperty || change.Property == TextProperty)
        {
            ToolTip.SetTip(this, IsCompact ? Text : null);
        }
    }

    private void ExecuteCommand()
    {
        if (Command?.CanExecute(null) == true)
        {
            Command.Execute(null);
        }
    }
}
