using System.Threading.Tasks;

namespace Mostawsef.Desktop.ViewModels.Shell;

/// <summary>
/// A page that loads data. The portal shell keeps the page while the portal is open, activates it every time
/// it is shown and deactivates it when the user leaves it or the portal closes.
/// </summary>
public interface ILoadablePage
{
    /// <summary>Loads the data the first time, and again if the last load was interrupted by Deactivate.</summary>
    Task ActivateAsync();

    /// <summary>Cancels the page's API calls that are still running.</summary>
    void Deactivate();
}
