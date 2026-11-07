using Avalonia.Controls;
using Avalonia.Input;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.Views.Shell;

public partial class PortalShellView : UserControl
{
    public PortalShellView()
    {
        InitializeComponent();
    }

    private void OnSignOutTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is PortalShellViewModel vm && vm.SignOutCommand.CanExecute(null))
        {
            vm.SignOutCommand.Execute(null);
        }
    }
}
