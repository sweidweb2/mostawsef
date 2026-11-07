using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.Views.Shell;

public partial class PortalShellView : UserControl
{
    public PortalShellView()
    {
        InitializeComponent();
    }

    private void OnSignOutTapped(object? sender, TappedEventArgs e) => SignOut();

    private void OnSignOutClick(object? sender, RoutedEventArgs e) => SignOut();

    private void SignOut()
    {
        if (DataContext is PortalShellViewModel vm && vm.SignOutCommand.CanExecute(null))
        {
            vm.SignOutCommand.Execute(null);
        }
    }
}
