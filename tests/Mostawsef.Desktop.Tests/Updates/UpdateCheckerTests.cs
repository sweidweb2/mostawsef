using System.Net;
using System.Text;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Updates;
using Mostawsef.Desktop.Tests.Infrastructure;

namespace Mostawsef.Desktop.Tests.Updates;

[Collection(UiTestCollection.Name)]
public class UpdateCheckerTests : UiTestBase
{
    [AvaloniaFact]
    public async Task A_newer_feed_version_marks_an_update_available()
    {
        using var app = new TestServices();
        app.Updates.Error = null;
        app.Updates.RespondWithSetup("99.0.0", Encoding.UTF8.GetBytes("setup-bytes"));

        var checker = app.Get<UpdateChecker>();
        await checker.CheckNowAsync(force: true);
        Dispatcher.UIThread.RunJobs();

        Assert.True(checker.IsAvailable);
        Assert.Contains("99.0.0", checker.BannerText);
    }

    [AvaloniaFact]
    public async Task Same_or_older_feed_hides_the_banner()
    {
        using var app = new TestServices();
        app.Updates.Error = null;
        app.Updates.RespondWith(new UpdateFeed(
            AppVersion.Current,
            UpdateChecker.AllowedDownloadPrefix + "v1/setup.exe",
            "abc",
            "notes",
            null));

        var checker = app.Get<UpdateChecker>();
        await checker.CheckNowAsync(force: true);
        Dispatcher.UIThread.RunJobs();

        Assert.False(checker.IsAvailable);
        Assert.Empty(checker.BannerText);
    }

    [AvaloniaFact]
    public async Task Network_errors_stay_quiet()
    {
        using var app = new TestServices();

        var checker = app.Get<UpdateChecker>();
        await checker.CheckNowAsync(force: true);
        Dispatcher.UIThread.RunJobs();

        Assert.False(checker.IsAvailable);
        Assert.True(app.Updates.RequestCount >= 1);
    }

    [AvaloniaFact]
    public async Task Invalid_json_stays_quiet()
    {
        using var app = new TestServices();
        app.Updates.Error = null;
        app.Updates.JsonBody = "<html>nope</html>";

        var checker = app.Get<UpdateChecker>();
        await checker.CheckNowAsync(force: true);
        Dispatcher.UIThread.RunJobs();

        Assert.False(checker.IsAvailable);
    }

    [AvaloniaFact]
    public async Task Feed_404_stays_quiet()
    {
        using var app = new TestServices();
        app.Updates.Error = null;
        app.Updates.StatusCode = HttpStatusCode.NotFound;
        app.Updates.JsonBody = "{}";

        var checker = app.Get<UpdateChecker>();
        await checker.CheckNowAsync(force: true);
        Dispatcher.UIThread.RunJobs();

        Assert.False(checker.IsAvailable);
    }

    [AvaloniaFact]
    public async Task Feed_without_sha256_is_ignored()
    {
        using var app = new TestServices();
        app.Updates.Error = null;
        app.Updates.RespondWith(new UpdateFeed(
            "99.0.0",
            UpdateChecker.AllowedDownloadPrefix + "v99.0.0/Mostawsef-Setup-99.0.0.exe",
            "",
            "notes",
            null));

        var checker = app.Get<UpdateChecker>();
        await checker.CheckNowAsync(force: true);
        Dispatcher.UIThread.RunJobs();

        Assert.False(checker.IsAvailable);
    }

    [AvaloniaFact]
    public async Task Second_install_while_busy_does_not_start_twice()
    {
        using var app = new TestServices();
        app.Updates.Error = null;
        app.Updates.RespondWithSetup("99.0.0", Encoding.UTF8.GetBytes("setup"));

        var checker = app.Get<UpdateChecker>();
        await checker.CheckNowAsync(force: true);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        checker.ConfirmLeaveAsync = async () =>
        {
            gate.SetResult();
            await Task.Delay(200);
            return true;
        };

        var first = checker.InstallCommand.ExecuteAsync(null);
        await gate.Task;
        Assert.False(checker.InstallCommand.CanExecute(null));
        await first;
        Dispatcher.UIThread.RunJobs();

        Assert.Single(app.UpdateHost.ApplyCalls);
    }

    [AvaloniaFact]
    public async Task Install_downloads_verifies_and_starts_the_applicator()
    {
        using var app = new TestServices();
        app.Updates.Error = null;
        var bytes = Encoding.UTF8.GetBytes("mostawsef-setup-payload");
        app.Updates.RespondWithSetup("99.0.0", bytes);

        var checker = app.Get<UpdateChecker>();
        await checker.CheckNowAsync(force: true);
        Dispatcher.UIThread.RunJobs();

        await checker.InstallCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(app.UpdateHost.ApplyCalls);
        Assert.Equal(1, app.UpdateHost.ExitCalls);
        Assert.True(File.Exists(app.UpdateHost.ApplyCalls[0].SetupPath));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(app.UpdateHost.ApplyCalls[0].SetupPath));
        Assert.Equal(FakeUpdateHandler.Sha256Hex(bytes), app.UpdateHost.ApplyCalls[0].Sha256, ignoreCase: true);
        Assert.True(app.Updates.FileRequestCount >= 1);
    }

    [AvaloniaFact]
    public async Task Bad_checksum_does_not_start_setup()
    {
        using var app = new TestServices();
        app.Updates.Error = null;
        var bytes = Encoding.UTF8.GetBytes("payload");
        var feed = app.Updates.RespondWithSetup("99.0.0", bytes);
        app.Updates.RespondWith(feed with { Sha256 = "0000000000000000000000000000000000000000000000000000000000000000" });
        app.Updates.FileBody = bytes;

        var checker = app.Get<UpdateChecker>();
        await checker.CheckNowAsync(force: true);
        await checker.InstallCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(app.UpdateHost.ApplyCalls);
        Assert.Equal(0, app.UpdateHost.ExitCalls);
        Assert.Contains("damaged", checker.ErrorText ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public async Task Cancel_during_download_keeps_the_old_app()
    {
        using var app = new TestServices();
        app.Updates.Error = null;
        // Large enough that we can cancel; handler returns synchronously so cancel after start still works via CTS.
        app.Updates.RespondWithSetup("99.0.0", new byte[1024 * 256]);

        var checker = app.Get<UpdateChecker>();
        await checker.CheckNowAsync(force: true);
        checker.ConfirmLeaveAsync = () =>
        {
            checker.CancelInstallCommand.Execute(null);
            return Task.FromResult(true);
        };

        // Cancel before download by aborting leave? Better: cancel after install starts via ConfirmLeave staying true
        // and CancelInstall from a progress callback — use ConfirmLeave that cancels the CTS mid-flight.
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        checker.ConfirmLeaveAsync = async () =>
        {
            started.SetResult();
            await Task.Yield();
            checker.CancelInstallCommand.Execute(null);
            return true;
        };

        var install = checker.InstallCommand.ExecuteAsync(null);
        await started.Task;
        await install;
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(app.UpdateHost.ApplyCalls);
        Assert.Equal(0, app.UpdateHost.ExitCalls);
    }

    [AvaloniaFact]
    public async Task ConfirmLeave_false_aborts_without_downloading()
    {
        using var app = new TestServices();
        app.Updates.Error = null;
        app.Updates.RespondWithSetup("99.0.0", Encoding.UTF8.GetBytes("x"));

        var checker = app.Get<UpdateChecker>();
        await checker.CheckNowAsync(force: true);
        checker.ConfirmLeaveAsync = () => Task.FromResult(false);

        await checker.InstallCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, app.Updates.FileRequestCount);
        Assert.Empty(app.UpdateHost.ApplyCalls);
        Assert.False(checker.IsInstalling);
    }

    [AvaloniaFact]
    public async Task Throttle_skips_a_second_check_inside_the_interval_and_checks_again_after_it()
    {
        using var app = new TestServices();
        app.Updates.Error = null;
        app.Updates.RespondWithSetup("99.0.0", Encoding.UTF8.GetBytes("x"));

        var checker = app.Get<UpdateChecker>();
        await checker.CheckNowAsync(force: true);
        var first = app.Updates.RequestCount;

        app.UpdateClock.Advance(UpdateChecker.MinCheckInterval - TimeSpan.FromMinutes(1));
        await checker.CheckNowAsync(force: false);
        Assert.Equal(first, app.Updates.RequestCount);

        app.UpdateClock.Advance(TimeSpan.FromMinutes(1));
        await checker.CheckNowAsync(force: false);
        Assert.Equal(first + 1, app.Updates.RequestCount);
    }

    [AvaloniaFact]
    public async Task An_unexpected_failure_is_thrown_instead_of_hidden()
    {
        using var app = new TestServices();
        app.Updates.Error = new InvalidOperationException("bug in the handler");

        var checker = app.Get<UpdateChecker>();

        // Only offline / timeout / bad feed mean "no update"; a bug must reach ErrorReporter (via the portal).
        await Assert.ThrowsAsync<InvalidOperationException>(() => checker.CheckNowAsync(force: true));
    }

    [AvaloniaFact]
    public async Task A_disposed_checker_no_longer_follows_the_language()
    {
        using var app = new TestServices();
        app.Updates.Error = null;
        app.Updates.RespondWithSetup("99.0.0", Encoding.UTF8.GetBytes("x"), notesEn: "English notes", notesAr: "ملاحظات عربية");
        var checker = app.Get<UpdateChecker>();
        await checker.CheckNowAsync(force: true);
        Dispatcher.UIThread.RunJobs();
        var english = checker.BannerText;

        checker.Dispose();
        app.Get<LocalizationService>().SetLanguage(AppLanguage.Arabic);

        Assert.Equal(english, checker.BannerText);
    }

    [AvaloniaFact]
    public async Task Arabic_banner_uses_arabic_notes()
    {
        using var app = new TestServices(AppLanguage.Arabic);
        app.Updates.Error = null;
        app.Updates.RespondWithSetup("99.0.0", Encoding.UTF8.GetBytes("x"), notesEn: "English notes", notesAr: "ملاحظات عربية");

        var checker = app.Get<UpdateChecker>();
        await checker.CheckNowAsync(force: true);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("ملاحظات عربية", checker.BannerText);
        Assert.StartsWith("الإصدار", checker.BannerText);
    }

    [Fact]
    public void TryParseArgs_reads_setup_path_hash_and_wait_pid()
    {
        Assert.True(UpdateApplicator.TryParseArgs(
            ["Mostawsef.Desktop.exe", "--apply-update", @"C:\Temp\setup.exe", "--sha256", "abc123", "--wait-pid", "1234"],
            out var path,
            out var sha256,
            out var pid));
        Assert.Equal(@"C:\Temp\setup.exe", path);
        Assert.Equal("abc123", sha256);
        Assert.Equal(1234, pid);
    }

    [Theory]
    [InlineData("http://github.com/sweidweb2/mostawsef/releases/download/v99.0.0/Mostawsef-Setup-99.0.0.exe")]
    [InlineData("https://example.com/Mostawsef-Setup-99.0.0.exe")]
    [InlineData("https://github.com/someone-else/mostawsef/releases/download/v99.0.0/Mostawsef-Setup-99.0.0.exe")]
    [InlineData("https://github.com.evil.example/sweidweb2/mostawsef/releases/download/v99.0.0/Mostawsef-Setup-99.0.0.exe")]
    [InlineData("https://github.com:8443/sweidweb2/mostawsef/releases/download/v99.0.0/Mostawsef-Setup-99.0.0.exe")]
    [InlineData("https://user@github.com/sweidweb2/mostawsef/releases/download/v99.0.0/Mostawsef-Setup-99.0.0.exe")]
    public void A_setup_url_outside_this_repository_releases_is_not_allowed(string url)
    {
        Assert.False(UpdateChecker.IsAllowedDownloadUrl(url));
    }

    [AvaloniaFact]
    public async Task A_feed_pointing_at_another_host_shows_no_update()
    {
        using var app = new TestServices();
        app.Updates.Error = null;
        var feed = app.Updates.RespondWithSetup("99.0.0", Encoding.UTF8.GetBytes("setup"));
        app.Updates.RespondWith(feed with { Url = "https://example.com/Mostawsef-Setup-99.0.0.exe" });

        var checker = app.Get<UpdateChecker>();
        await checker.CheckNowAsync(force: true);
        Dispatcher.UIThread.RunJobs();

        Assert.False(checker.IsAvailable);
        Assert.False(checker.InstallCommand.CanExecute(null));
    }
}
