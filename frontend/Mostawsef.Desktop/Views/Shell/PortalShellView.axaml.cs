using System;
using Avalonia.Controls;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.ViewModels;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.Views.Shell;

public partial class PortalShellView : UserControl
{
    private PortalShellViewModel? _shell;
    private PageHost? _pageHost;

    public PortalShellView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) => _pageHost ??= this.FindControl<PageHost>("PageHost");
        DetachedFromVisualTree += (_, _) => Unsubscribe();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Unsubscribe();
        _shell = DataContext as PortalShellViewModel;
        if (_shell is null)
        {
            return;
        }

        _shell.PageDiscarded += OnPageDiscarded;
    }

    private void Unsubscribe()
    {
        if (_shell is not null)
        {
            _shell.PageDiscarded -= OnPageDiscarded;
            _shell = null;
        }
    }

    private void OnPageDiscarded(ViewModelBase page)
    {
        _pageHost ??= this.FindControl<PageHost>("PageHost");
        _pageHost?.Release(page);
    }
}
