using Mostawsef.Desktop.Services.Updates;

namespace Mostawsef.Desktop.Tests.Infrastructure;

public sealed class FakeUpdateHost : IUpdateHost
{
    public FakeUpdateHost(string downloadDirectory)
    {
        DownloadDirectory = downloadDirectory;
        Directory.CreateDirectory(downloadDirectory);
    }

    public string DownloadDirectory { get; }

    public List<(string SetupPath, string Sha256, int Pid)> ApplyCalls { get; } = [];

    public int ExitCalls { get; private set; }

    public void StartApplyUpdate(string setupPath, string sha256, int currentProcessId) =>
        ApplyCalls.Add((setupPath, sha256, currentProcessId));

    public void ExitApplication() => ExitCalls++;
}
