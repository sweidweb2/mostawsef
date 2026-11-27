using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Updates;
using Mostawsef.Desktop.Tests.Infrastructure;

namespace Mostawsef.Desktop.Tests.Updates;

[Collection(UiTestCollection.Name)]
public class UpdateCheckerTests : UiTestBase
{
    public UpdateCheckerTests()
    {
        UpdateChecker.MinCheckInterval = TimeSpan.Zero;
    }

    [AvaloniaFact]
    public async Task A_newer_feed_version_marks_an_update_available()
    {
        using var app = new TestServices();
        app.Updates.Error = null;
        app.Updates.RespondWith(NewerFeed());

        var checker = app.Get<UpdateChecker>();
        await checker.CheckNowAsync(force: true);
        Dispatcher.UIThread.RunJobs();

        Assert.True(checker.IsAvailable);
        Assert.Contains("99.0.0", checker.BannerText);
        Assert.Equal(NewerFeed().Url, checker.DownloadUrl);
    }

    [AvaloniaFact]
    public async Task Same_or_older_feed_hides_the_banner()
    {
        using var app = new TestServices();
        app.Updates.Error = null;
        app.Updates.RespondWith(new UpdateFeed(
            AppVersion.Current,
            "https://example.com/setup.exe",
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
        // Default handler already fails the network.

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
    public async Task Download_opens_the_setup_url()
    {
        using var app = new TestServices();
        app.Updates.Error = null;
        var feed = NewerFeed();
        app.Updates.RespondWith(feed);

        var checker = app.Get<UpdateChecker>();
        await checker.CheckNowAsync(force: true);
        Dispatcher.UIThread.RunJobs();

        Assert.True(checker.DownloadCommand.CanExecute(null));
        checker.DownloadCommand.Execute(null);

        Assert.Equal([feed.Url], app.OpenedUrls);
    }

    [AvaloniaFact]
    public async Task Throttle_skips_a_second_check_inside_the_interval()
    {
        UpdateChecker.MinCheckInterval = TimeSpan.FromHours(1);
        using var app = new TestServices();
        app.Updates.Error = null;
        app.Updates.RespondWith(NewerFeed());

        var checker = app.Get<UpdateChecker>();
        await checker.CheckNowAsync(force: true);
        var first = app.Updates.RequestCount;

        await checker.CheckNowAsync(force: false);

        Assert.Equal(first, app.Updates.RequestCount);
    }

    [AvaloniaFact]
    public async Task Arabic_banner_uses_arabic_notes()
    {
        using var app = new TestServices(AppLanguage.Arabic);
        app.Updates.Error = null;
        app.Updates.RespondWith(new UpdateFeed(
            "99.0.0",
            "https://example.com/Mostawsef-Setup.exe",
            "deadbeef",
            "English notes",
            "ملاحظات عربية"));

        var checker = app.Get<UpdateChecker>();
        await checker.CheckNowAsync(force: true);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("ملاحظات عربية", checker.BannerText);
        Assert.StartsWith("الإصدار", checker.BannerText);
    }

    private static UpdateFeed NewerFeed() => new(
        "99.0.0",
        "https://github.com/sweidweb2/mostawsef/releases/download/v99.0.0/Mostawsef-Setup-99.0.0.exe",
        "abc123",
        "Bug fixes",
        "إصلاحات");
}
