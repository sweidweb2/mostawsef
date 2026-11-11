using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Super;
using static Mostawsef.Desktop.Tests.Infrastructure.TestServices;

namespace Mostawsef.Desktop.Tests.Views;

[Collection(UiTestCollection.Name)]
public class TranslationBindingTests : UiTestBase
{
    private static readonly ActivityLogListItemDto SignIn =
        new(Guid.NewGuid(), DateTime.UtcNow, "auth.signed_in", "Auth", "Signed in", "admin@test.local");

    [AvaloniaFact]
    public async Task Switching_language_retranslates_plain_texts_table_cells_and_formatted_texts()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        app.Api
            .OnGet("/api/activity-logs/stats", new ActivityLogStatsDto(3, 1, 3, 0))
            .OnGet("/api/activity-logs", new CursorPage<ActivityLogListItemDto>([SignIn], 20, 3, null, null));
        var page = app.Get<ActivityLogsViewModel>();
        await page.ActivateAsync();
        var window = app.Show(page);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Activity logs", Texts(window));
        Assert.Equal("Signed in", ActionCell(window).Text);
        Assert.Contains("3 records", Texts(window));

        app.Localization.SetLanguage(AppLanguage.Arabic);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("سجل النشاطات", Texts(window));
        Assert.Equal("تسجيل دخول", ActionCell(window).Text);
        Assert.Contains("3 سجل", Texts(window));
    }

    private static TextBlock ActionCell(Window window) =>
        window.GetVisualDescendants().OfType<TextBlock>().Single(t => Equals(ToolTip.GetTip(t), SignIn.Action));
}
