using Mostawsef.Desktop.Services.Updates;

namespace Mostawsef.Desktop.Tests.Infrastructure;

public sealed class FakeUpdateHost : IUpdateHost
{
    public FakeUpdateHost(string downloadDirectory)
    {
        DownloadDirectory = downloadDirectory;
        Directory.CreateDirectory(downloadDirectory);
    }

    public string DesktopExePath { get; set; } = Path.Combine(Path.GetTempPath(), "Mostawsef.Desktop.exe");

    public string DownloadDirectory { get; }

    public string InstallClinicLogPath { get; set; } = Path.Combine(Path.GetTempPath(), "install-clinic.log");

    public List<(string SetupPath, int Pid)> ApplyCalls { get; } = [];

    public int ExitCalls { get; private set; }

    public void StartApplyUpdate(string setupPath, int currentProcessId) =>
        ApplyCalls.Add((setupPath, currentProcessId));

    public void ExitApplication() => ExitCalls++;
}
