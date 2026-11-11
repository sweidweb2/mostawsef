using Avalonia.Threading;

namespace Mostawsef.Desktop.Tests.Infrastructure;

/// <summary>
/// Waits for async UI/view-model work. Mirrors the previous per-file WaitUntil loops:
/// pump the dispatcher on the Avalonia UI thread, then delay — never InvokeAsync (that deadlocks
/// while AvaloniaFact is blocked on the test task).
/// </summary>
public static class UiWait
{
    public const int DefaultAttempts = 200;

    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    /// <param name="pumpDispatcher">False for waits that do not touch Avalonia (e.g. pure HTTP in-flight counts).</param>
    public static async Task UntilAsync(Func<bool> condition, bool pumpDispatcher = true)
    {
        var cancellation = TestContext.Current.CancellationToken;

        for (var i = 0; i < DefaultAttempts && !condition(); i++)
        {
            if (pumpDispatcher)
            {
                Dispatcher.UIThread.RunJobs();
            }

            await Task.Delay(PollInterval, cancellation);
        }

        if (pumpDispatcher)
        {
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(condition());
    }

    /// <summary>Pumps the dispatcher a few times so late callbacks can finish before an assertion.</summary>
    public static async Task DrainAsync(int rounds = 20)
    {
        var cancellation = TestContext.Current.CancellationToken;
        for (var i = 0; i < rounds; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(PollInterval, cancellation);
        }
    }
}
