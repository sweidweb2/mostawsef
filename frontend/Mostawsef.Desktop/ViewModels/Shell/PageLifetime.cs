using System;
using System.Threading;

namespace Mostawsef.Desktop.ViewModels.Shell;

/// <summary>
/// Cancels in-flight page work on leave and recreates a fresh token on the next show.
/// Shared by numbered/cursor list pages and New order.
/// </summary>
internal sealed class PageLifetime : IDisposable
{
    private CancellationTokenSource _cts = new();

    public CancellationToken Token => _cts.Token;

    public bool IsCancellationRequested => _cts.IsCancellationRequested;

    /// <summary>Call at the start of Activate: replaces a cancelled source so work can run again.</summary>
    public void EnsureActive()
    {
        if (!_cts.IsCancellationRequested)
        {
            return;
        }

        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    /// <summary>Call on Deactivate / reuse reset: cancel and dispose so wait handles are not left until GC.</summary>
    public void Cancel()
    {
        if (!_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }

        _cts.Dispose();
        _cts = new CancellationTokenSource();
        _cts.Cancel();
    }

    public void Dispose() => _cts.Dispose();
}
