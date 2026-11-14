namespace Mostawsef.Application.Common;

public static class TaskObserver
{
    /// <summary>
    /// Awaits a task that was started alongside other work and ignores its failure. Call it only on a path that is already
    /// failing with its own exception: the racing task must still be observed so its fault is not left unobserved,
    /// but it must not replace the exception being thrown.
    /// </summary>
    public static async Task ObserveAsync(this Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task;
        }
        catch
        {
            // Intentionally ignored: see the summary.
        }
    }
}
