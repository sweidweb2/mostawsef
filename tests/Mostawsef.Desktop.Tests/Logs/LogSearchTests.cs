using Avalonia.Headless.XUnit;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Super;
using NSubstitute;

namespace Mostawsef.Desktop.Tests.Logs;

[Collection(UiTestCollection.Name)]
public class LogSearchTests : UiTestBase
{
    private const string ListPath = "/api/activity-logs";

    [AvaloniaTheory]
    [InlineData("ab")]
    [InlineData(" a b ")]
    [InlineData("ـمـ")]
    public async Task A_term_with_fewer_than_3_letters_is_not_sent_and_a_warning_explains_why(string term)
    {
        using var app = new TestServices();
        var (page, toasts) = await OpenActivityLogsAsync(app);
        var requests = app.Api.RequestsTo(HttpMethod.Get, ListPath).Count();

        page.Search = term;
        await page.SearchCommand.ExecuteAsync(null);

        Assert.Equal(requests, app.Api.RequestsTo(HttpMethod.Get, ListPath).Count());
        toasts.Received(1).Show("Type at least 3 letters or numbers to search.", null, ToastKind.Warning, null);
        Assert.Single(page.Items);
    }

    [AvaloniaFact]
    public async Task A_term_with_3_letters_is_sent()
    {
        using var app = new TestServices();
        var (page, toasts) = await OpenActivityLogsAsync(app);

        page.Search = "abc";
        await page.SearchCommand.ExecuteAsync(null);

        Assert.Contains(app.Api.RequestsTo(HttpMethod.Get, ListPath), r => r.RequestUri!.Query.Contains("search=abc", StringComparison.Ordinal));
        toasts.DidNotReceiveWithAnyArgs().Show(default!);
    }

    [AvaloniaFact]
    public void The_warning_has_an_arabic_translation()
    {
        using var app = new TestServices(AppLanguage.Arabic);

        Assert.Equal("اكتب 3 أحرف أو أرقام على الأقل للبحث.", app.Localization.Format("Search.TooShort", CursorQuery.MinSearchLength));
    }

    private static async Task<(ActivityLogsViewModel Page, IToastService Toasts)> OpenActivityLogsAsync(TestServices app)
    {
        var item = new ActivityLogListItemDto(Guid.NewGuid(), DateTime.UtcNow, "auth.signed_in", "Auth", "Signed in", "admin@test.local");
        app.Api.OnGet($"{ListPath}/stats", new ActivityLogStatsDto(1, 1, 1, 0));
        app.Api.OnGet(ListPath, new CursorPage<ActivityLogListItemDto>([item], 20, 1, null, null));
        app.SignInAs(Roles.Super);

        var toasts = Substitute.For<IToastService>();
        var page = new ActivityLogsViewModel(app.Get<LogsApi>(), app.Get<IDialogService>(), toasts, app.Localization, app.Get<ErrorReporter>());
        await page.ActivateAsync();
        return (page, toasts);
    }
}
