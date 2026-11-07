using Mostawsef.Desktop.Services.Navigation;

namespace Mostawsef.Desktop.ViewModels;

public class MainWindowViewModel(NavigationService navigation) : ViewModelBase
{
    public NavigationService Navigation { get; } = navigation;
}
