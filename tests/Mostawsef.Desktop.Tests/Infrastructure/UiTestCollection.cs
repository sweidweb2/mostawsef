namespace Mostawsef.Desktop.Tests.Infrastructure;

/// <summary>
/// Marks tests that swap <see cref="Mostawsef.Desktop.App.Services"/> (and process culture).
/// The assembly still disables parallelization (Avalonia headless cannot share the process with
/// other collections); this collection documents which classes own that shared state.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class UiTestCollection
{
    public const string Name = "ui";
}

/// <summary>
/// Marker base for desktop tests that construct <see cref="TestServices"/> or render Avalonia UI.
/// Each subclass also declares <c>[Collection(UiTestCollection.Name)]</c>.
/// </summary>
public abstract class UiTestBase;
