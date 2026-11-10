using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Navigation;

namespace Mostawsef.Desktop.ViewModels;

public class MainWindowViewModel(NavigationService navigation, PerformanceMonitor perf) : ViewModelBase
{
    public NavigationService Navigation { get; } = navigation;

    public PerformanceMonitor Perf { get; } = perf;
}
