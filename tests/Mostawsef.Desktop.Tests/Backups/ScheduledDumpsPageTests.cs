using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Backups;
using Mostawsef.Contracts.Common;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Super;
using static Mostawsef.Desktop.Tests.Infrastructure.TestServices;

namespace Mostawsef.Desktop.Tests.Backups;

[Collection(UiTestCollection.Name)]
public class ScheduledDumpsPageTests : UiTestBase
{
    private const string BackupsPath = "/api/backups";
    private const string DumpsPath = "/api/scheduled-dumps";

    private static readonly ScheduledDumpDto Sample = new(
        Guid.NewGuid(),
        @"D:\Mostawsef\Backups",
        @"D:\Mostawsef\Backups\mostawsef-20260101-120000.dump",
        2_048_000,
        KeepCount: 7,
        IntervalDays: 1,
        DeletedOldFiles: 2,
        CreatedAt: DateTime.UtcNow.AddHours(-3));

    [AvaloniaFact]
    public void Dump_history_is_on_backups_not_a_separate_menu_page()
    {
        using var app = new TestServices();
        Assert.False(app.Navigation.CanAccess(typeof(ScheduledDumpsViewModel)));

        app.SignInAs(Roles.Admin);
        Assert.False(app.Navigation.CanAccess(typeof(ScheduledDumpsViewModel)));

        app.SignInAs(Roles.Super);
        Assert.True(app.Navigation.CanAccess(typeof(BackupsViewModel)));
        Assert.True(app.Navigation.CanAccess(typeof(ScheduledDumpsViewModel)));
        app.Navigation.NavigateToHome();
        var shell = Assert.IsType<Mostawsef.Desktop.ViewModels.Shell.PortalShellViewModel>(app.Navigation.CurrentPage);
        Assert.DoesNotContain(shell.MenuItems, i => i.TitleKey == "Nav.ScheduledDumps");
        Assert.Contains(shell.MenuItems, i => i.TitleKey == "Nav.Backups");
    }

    [AvaloniaFact]
    public async Task The_backups_page_shows_when_where_and_settings_for_each_dump()
    {
        using var app = SuperWithDump(Sample);
        var (page, window) = await ShowBackups(app);

        Assert.Single(page.Dumps.Items);
        var texts = Texts(window);
        Assert.Contains(@"D:\Mostawsef\Backups", texts);
        Assert.Contains("mostawsef-20260101-120000.dump", texts);
        Assert.Contains("1 dumps", texts);
        Assert.Equal(2_048_000, page.Dumps.Items[0].FileSizeBytes);
        Assert.Contains(texts, t => t.Contains("MB", StringComparison.Ordinal));
        Assert.Contains("7", texts);
        Assert.Contains("Auto backups", texts);
        Assert.Equal("Backups", window.GetVisualDescendants().OfType<PageHeader>().Single().Title);
    }

    [AvaloniaFact]
    public async Task Refresh_reloads_export_history_and_dump_history()
    {
        var dumps = new List<ScheduledDumpDto> { Sample };
        using var app = SuperWithDump(dumps);
        var (page, _) = await ShowBackups(app);
        Assert.Single(page.Dumps.Items);

        dumps.Clear();
        await page.RefreshCommand.ExecuteAsync(null);

        Assert.Empty(page.Dumps.Items);
    }

    private static TestServices SuperWithDump(IReadOnlyList<ScheduledDumpDto> items)
    {
        var app = new TestServices();
        app.SignInAs(Roles.Super);
        app.Api.OnGet($"{BackupsPath}/tables", Array.Empty<string>());
        app.Api.OnGet(BackupsPath, new PagedResult<BackupRecordDto>([], 1, 20, 0));
        app.Api.On(HttpMethod.Get, DumpsPath, _ =>
            FakeApiHandler.Json(new PagedResult<ScheduledDumpDto>(items.ToList(), 1, 50, items.Count)));
        return app;
    }

    private static TestServices SuperWithDump(params ScheduledDumpDto[] items) => SuperWithDump((IReadOnlyList<ScheduledDumpDto>)items);

    private static async Task<(BackupsViewModel Page, Window Window)> ShowBackups(TestServices app)
    {
        var page = app.Get<BackupsViewModel>();
        await page.ActivateAsync();
        var host = new DialogHost { Content = new ContentControl { Content = page } };
        app.Get<DialogService>().Attach(host);
        var window = app.Show(host);
        Dispatcher.UIThread.RunJobs();
        return (page, window);
    }
}
