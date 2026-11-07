using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Mostawsef.Desktop.Views.Auth;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        UsernameBox.Focus();
    }
}
