using System.ComponentModel;

namespace Mostawsef.Desktop.ViewModels.Shell;

/// <summary>
/// A portal sub-page that names itself in the top bar breadcrumb (menu page &gt; this page).
/// Raise PropertyChanged for <see cref="BreadcrumbArg"/> when it changes (e.g. once the order loads).
/// </summary>
public interface IBreadcrumbPage : INotifyPropertyChanged
{
    /// <summary>Resource key of the breadcrumb text.</summary>
    string BreadcrumbKey { get; }

    /// <summary>Optional {0} argument of <see cref="BreadcrumbKey"/>.</summary>
    string? BreadcrumbArg { get; }
}
