using System.Threading;

namespace Mostawsef.Desktop.ViewModels.Shell;

/// <summary>
/// "Only the newest load counts" for a details page: starting a load cancels the one still running, leaving the page
/// (or pointing it at another record) cancels it, and a load only touches the page's state if it is still the newest.
/// </summary>
public sealed class LatestLoad
{
    private CancellationTokenSource? _current;

    /// <summary>Cancels the running load and returns the token source of the new one; the caller disposes it.</summary>
    public CancellationTokenSource Begin()
    {
        _current?.Cancel();
        var next = new CancellationTokenSource();
        _current = next;
        return next;
    }

    /// <summary>True if <paramref name="load"/> is still the newest (it then owns the page's loading state); false if it was replaced or cancelled.</summary>
    public bool Finish(CancellationTokenSource load)
    {
        if (_current != load)
        {
            return false;
        }

        _current = null;
        return true;
    }

    /// <summary>Cancels the running load, if any.</summary>
    public void Cancel()
    {
        _current?.Cancel();
        _current = null;
    }
}
