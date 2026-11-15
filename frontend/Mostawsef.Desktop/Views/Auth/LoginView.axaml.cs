using Avalonia.Controls;
using Avalonia.Interactivity;
using Mostawsef.Desktop.Components;

namespace Mostawsef.Desktop.Views.Auth;

public partial class LoginView : UserControl
{
    private bool? _isNarrow;

    public LoginView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        UsernameBox.Focus();
    }

    // Same breakpoint as AppShell :narrow. Only touches the layout when the side of the breakpoint changes,
    // not on every resize step (no ContainerQuery: see performance rules).
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        var narrow = e.NewSize.Width < AppShell.NarrowContentWidth;
        if (_isNarrow == narrow)
        {
            return;
        }

        _isNarrow = narrow;
        BrandPanel.IsVisible = !narrow;
        Grid.SetColumn(FormSide, narrow ? 0 : 1);
        Grid.SetColumnSpan(FormSide, narrow ? 2 : 1);
    }
}
