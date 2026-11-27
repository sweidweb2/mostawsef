namespace Mostawsef.Desktop.Tests.Infrastructure;

/// <summary>A clock that only moves when the test calls <see cref="Advance"/>.</summary>
public sealed class ManualClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
