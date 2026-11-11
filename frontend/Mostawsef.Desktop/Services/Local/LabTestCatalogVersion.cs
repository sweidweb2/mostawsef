using System.Threading;

namespace Mostawsef.Desktop.Services.Local;

/// <summary>Bumped when lab tests are added, edited or deleted so New Order reloads its catalog.</summary>
public sealed class LabTestCatalogVersion
{
    private int _version;

    public int Current => Volatile.Read(ref _version);

    public void Invalidate() => Interlocked.Increment(ref _version);
}
