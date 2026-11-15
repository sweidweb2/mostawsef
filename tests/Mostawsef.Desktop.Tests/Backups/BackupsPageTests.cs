using System.Net;
using System.Net.Http.Json;
using System.Text;
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
public class BackupsPageTests : UiTestBase
{
    private const string BackupsPath = "/api/backups";
    private const string DumpsPath = "/api/scheduled-dumps";

    private static readonly string[] AllTables = ["backup_records", "patients", "users"];

    private static readonly BackupRecordDto Everything =
        new(Guid.NewGuid(), null, BackupFormats.PostgreSql, DateTime.UtcNow.AddDays(-2), "super@test.local");

    private static readonly BackupRecordDto Patients =
        new(Guid.NewGuid(), "patients", BackupFormats.Csv, DateTime.UtcNow.AddDays(-1), "super@test.local");

    [AvaloniaFact]
    public void Only_super_users_can_open_the_backups_page()
    {
        using var app = new TestServices();
        Assert.False(app.Navigation.CanAccess(typeof(BackupsViewModel)));

        app.SignInAs(Roles.Admin);
        Assert.False(app.Navigation.CanAccess(typeof(BackupsViewModel)));

        app.SignInAs(Roles.Super);
        Assert.True(app.Navigation.CanAccess(typeof(BackupsViewModel)));
    }

    [AvaloniaFact]
    public async Task The_history_shows_what_was_exported_and_in_which_format()
    {
        using var app = SuperWithHistory(Everything, Patients);

        var (page, window) = await ShowPage(app);

        Assert.Equal(2, page.Items.Count);
        var texts = Texts(window);
        Assert.Contains("All tables", texts);
        Assert.Contains("patients", texts);
        Assert.Contains("2 backups", texts);
        var badges = window.GetVisualDescendants().OfType<Badge>().Where(b => b.IsEffectivelyVisible).Select(b => b.Text).ToList();
        Assert.Contains("PostgreSQL", badges);
        Assert.Contains("CSV", badges);
        Assert.Equal(2, ExportRowButtons(window).Count);
    }

    [AvaloniaFact]
    public async Task The_form_lists_the_tables_and_every_format()
    {
        using var app = SuperWithHistory();

        var (page, window) = await ShowPage(app);

        Assert.Equal(AllTables, page.Tables);
        Assert.True(page.IsAllTables);
        Assert.Equal(["Csv", "Json", "Sql", "PostgreSql"], BackupsViewModel.FormatOptions.Select(o => o.Format));
        Assert.Equal(BackupFormats.All, BackupsViewModel.FormatOptions.Select(o => o.Format));
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<FormField>(), f => f.Label == "Table" && f.IsEffectivelyVisible);

        Named<RadioButton>(window, "OneTableOption").IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.False(page.IsAllTables);
        Assert.True(Named<ComboBox>(window, "TableBox").IsEffectivelyVisible);
        Assert.Equal("backup_records", page.SelectedTable);
    }

    [AvaloniaFact]
    public async Task The_format_hint_follows_the_chosen_format()
    {
        using var app = SuperWithHistory();
        var (page, window) = await ShowPage(app);

        page.SelectedFormat = BackupsViewModel.FormatOptions.Single(o => o.Format == BackupFormats.PostgreSql);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Texts(window), t => t.StartsWith("A script for PostgreSQL", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task Exporting_one_table_asks_where_to_save_then_stores_the_settings_and_writes_the_file()
    {
        using var app = SuperWithHistory();
        var created = new BackupRecordDto(Guid.NewGuid(), "patients", BackupFormats.Json, DateTime.UtcNow, "super@test.local");
        CreateBackupRequest? sent = null;
        app.Api.On(HttpMethod.Post, BackupsPath, request =>
        {
            sent = request.Content!.ReadFromJsonAsync<CreateBackupRequest>().GetAwaiter().GetResult();
            return FakeApiHandler.Json(created);
        });
        app.Api.On(HttpMethod.Get, $"{BackupsPath}/{created.Id}/export", _ => File("""{"tables":{}}"""));
        var (page, window) = await ShowPage(app);
        page.IsAllTables = false;
        page.SelectedTable = "patients";
        page.SelectedFormat = BackupsViewModel.FormatOptions.Single(o => o.Format == BackupFormats.Json);
        app.PickedSavePath = Path.Combine(app.DownloadsFolder, "chosen", "patients.json");
        Directory.CreateDirectory(Path.GetDirectoryName(app.PickedSavePath)!);

        await page.ExportNewCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        var suggestion = Assert.Single(app.SaveSuggestions);
        Assert.StartsWith("mostawsef-patients-", suggestion);
        Assert.EndsWith(".json", suggestion);
        Assert.Equal(("patients", BackupFormats.Json), (sent!.Table, sent.Format));
        Assert.Equal("""{"tables":{}}""", await System.IO.File.ReadAllTextAsync(app.PickedSavePath));
        Assert.False(System.IO.File.Exists(app.PickedSavePath + ".part"));
        Assert.Equal(created.Id, page.Items[0].Id);
        Assert.Contains($"Backup saved: {app.PickedSavePath}", Toasts(window));
        Assert.False(page.IsExporting);
    }

    [AvaloniaFact]
    public async Task Exporting_all_tables_sends_no_table_and_suggests_a_name_with_all()
    {
        using var app = SuperWithHistory();
        var created = new BackupRecordDto(Guid.NewGuid(), null, BackupFormats.Csv, DateTime.UtcNow, "super@test.local");
        CreateBackupRequest? sent = null;
        app.Api.On(HttpMethod.Post, BackupsPath, request =>
        {
            sent = request.Content!.ReadFromJsonAsync<CreateBackupRequest>().GetAwaiter().GetResult();
            return FakeApiHandler.Json(created);
        });
        app.Api.On(HttpMethod.Get, $"{BackupsPath}/{created.Id}/export", _ => File("zip bytes"));
        var (page, _) = await ShowPage(app);
        app.PickedSavePath = Path.Combine(app.DownloadsFolder, "all.zip");
        Directory.CreateDirectory(app.DownloadsFolder);

        await page.ExportNewCommand.ExecuteAsync(null);

        Assert.Null(sent!.Table);
        var suggestion = Assert.Single(app.SaveSuggestions);
        Assert.StartsWith("mostawsef-all-", suggestion);
        Assert.EndsWith(".zip", suggestion);
        Assert.True(System.IO.File.Exists(app.PickedSavePath));
    }

    [AvaloniaFact]
    public async Task Cancelling_the_save_dialog_stores_nothing_and_exports_nothing()
    {
        using var app = SuperWithHistory(Patients);
        var (page, _) = await ShowPage(app);
        app.PickedSavePath = null;

        await page.ExportNewCommand.ExecuteAsync(null);

        Assert.Single(app.SaveSuggestions);
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Post, BackupsPath));
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Get, $"{BackupsPath}/{Patients.Id}/export"));
        Assert.Single(page.Items);
        Assert.False(page.IsExporting);
    }

    [AvaloniaFact]
    public async Task Export_on_a_history_row_asks_where_to_save_and_reuses_its_settings_without_a_new_row()
    {
        using var app = SuperWithHistory(Everything, Patients);
        app.Api.On(HttpMethod.Get, $"{BackupsPath}/{Patients.Id}/export", _ => File("a,b"));
        var (page, window) = await ShowPage(app);
        app.PickedSavePath = Path.Combine(app.DownloadsFolder, "again.csv");
        Directory.CreateDirectory(app.DownloadsFolder);

        ExportRowButtons(window)[1].Command!.Execute(ExportRowButtons(window)[1].CommandParameter);
        await WaitUntilAsync(() => !page.IsExporting && System.IO.File.Exists(app.PickedSavePath));

        var suggestion = Assert.Single(app.SaveSuggestions);
        Assert.StartsWith("mostawsef-patients-", suggestion);
        Assert.EndsWith(".csv", suggestion);
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Post, BackupsPath));
        Assert.Equal("a,b", await System.IO.File.ReadAllTextAsync(app.PickedSavePath));
        Assert.Equal(2, page.Items.Count);
    }

    [AvaloniaFact]
    public async Task A_postgresql_row_suggests_a_pg_sql_file_name()
    {
        using var app = SuperWithHistory(Everything);
        app.Api.On(HttpMethod.Get, $"{BackupsPath}/{Everything.Id}/export", _ => File("-- sql"));
        var (page, _) = await ShowPage(app);
        app.PickedSavePath = Path.Combine(app.DownloadsFolder, "restore.sql");
        Directory.CreateDirectory(app.DownloadsFolder);

        await page.ExportCommand.ExecuteAsync(Everything);

        var suggestion = Assert.Single(app.SaveSuggestions);
        Assert.StartsWith("mostawsef-all-", suggestion);
        Assert.EndsWith(".pg.sql", suggestion);
    }

    [AvaloniaFact]
    public async Task A_refused_setting_shows_the_error_and_downloads_nothing()
    {
        using var app = SuperWithHistory();
        app.Api.On(HttpMethod.Post, BackupsPath, _ => FakeApiHandler.Problem(HttpStatusCode.BadRequest, "validation", ("Format", "invalid")));
        var (page, window) = await ShowPage(app);
        app.PickedSavePath = Path.Combine(app.DownloadsFolder, "x.csv");

        await page.ExportNewCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(System.IO.File.Exists(app.PickedSavePath));
        Assert.Contains(app.Localization.Get("ErrorCode.validation"), Toasts(window));
        Assert.Empty(page.Items);
        Assert.False(page.IsExporting);
    }

    [AvaloniaFact]
    public async Task A_failed_download_leaves_no_file_and_says_so()
    {
        using var app = SuperWithHistory(Patients);
        app.Api.On(HttpMethod.Get, $"{BackupsPath}/{Patients.Id}/export", HttpStatusCode.InternalServerError);
        var (page, window) = await ShowPage(app);
        app.PickedSavePath = Path.Combine(app.DownloadsFolder, "broken.csv");
        Directory.CreateDirectory(app.DownloadsFolder);

        await page.ExportCommand.ExecuteAsync(Patients);
        Dispatcher.UIThread.RunJobs();

        Assert.False(System.IO.File.Exists(app.PickedSavePath));
        Assert.False(System.IO.File.Exists(app.PickedSavePath + ".part"));
        Assert.Contains(app.Localization.Get("Error.RequestFailed"), Toasts(window));
        Assert.False(page.IsExporting);
    }

    [AvaloniaFact]
    public async Task The_server_being_down_says_so()
    {
        using var app = SuperWithHistory(Patients);
        app.Api.On(HttpMethod.Get, $"{BackupsPath}/{Patients.Id}/export", _ => throw new HttpRequestException("Connection refused"));
        var (page, window) = await ShowPage(app);
        app.PickedSavePath = Path.Combine(app.DownloadsFolder, "down.csv");
        Directory.CreateDirectory(app.DownloadsFolder);

        await page.ExportCommand.ExecuteAsync(Patients);
        Dispatcher.UIThread.RunJobs();

        Assert.False(System.IO.File.Exists(app.PickedSavePath));
        Assert.Contains(app.Localization.Get("Login.ServerUnreachable"), Toasts(window));
    }

    [AvaloniaFact]
    public async Task Every_export_button_waits_while_an_export_runs()
    {
        using var app = SuperWithHistory(Patients);
        var release = new TaskCompletionSource();
        app.Api.On(HttpMethod.Get, $"{BackupsPath}/{Patients.Id}/export", async (_, _) =>
        {
            await release.Task;
            return File("done");
        });
        var (page, window) = await ShowPage(app);
        app.PickedSavePath = Path.Combine(app.DownloadsFolder, "slow.csv");
        Directory.CreateDirectory(app.DownloadsFolder);

        var running = page.ExportCommand.ExecuteAsync(Patients);
        Dispatcher.UIThread.RunJobs();

        Assert.True(page.IsExporting);
        Assert.False(page.ExportNewCommand.CanExecute(null));
        Assert.False(page.ExportCommand.CanExecute(Patients));
        Assert.False(Named<Button>(window, "ExportNewButton").Command!.CanExecute(null));

        release.SetResult();
        await running;
        Dispatcher.UIThread.RunJobs();

        Assert.False(page.IsExporting);
        Assert.True(page.ExportNewCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task The_page_works_in_arabic_with_translated_formats()
    {
        using var app = new TestServices(Mostawsef.Desktop.Services.Local.AppLanguage.Arabic);
        app.SignInAs(Roles.Super);
        app.Api.OnGet($"{BackupsPath}/tables", AllTables);
        app.Api.OnGet(BackupsPath, new PagedResult<BackupRecordDto>([Patients], 1, 20, 1));
        app.Api.OnGet(DumpsPath, new PagedResult<ScheduledDumpDto>([], 1, 20, 0));

        var (_, window) = await ShowPage(app);

        var texts = Texts(window);
        Assert.Contains("النسخ الاحتياطي", texts);
        Assert.Contains("كل الجداول", texts);
        Assert.Contains("1 نسخة", texts.Select(t => t.Replace("١", "1")));
        Assert.Contains("النسخ التلقائي", texts);
    }

    private static HttpResponseMessage File(string content) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(content)) };

    private static TestServices SuperWithHistory(params BackupRecordDto[] records)
    {
        var app = new TestServices();
        app.SignInAs(Roles.Super);
        app.Api.OnGet($"{BackupsPath}/tables", AllTables);
        app.Api.OnGet(BackupsPath, new PagedResult<BackupRecordDto>(records, 1, 20, records.Length));
        app.Api.OnGet(DumpsPath, new PagedResult<ScheduledDumpDto>([], 1, 20, 0));
        return app;
    }

    private static async Task<(BackupsViewModel Page, Window Window)> ShowPage(TestServices app)
    {
        var page = app.Get<BackupsViewModel>();
        await page.ActivateAsync();
        var host = new DialogHost { Content = new ContentControl { Content = page } };
        app.Get<DialogService>().Attach(host);
        var window = app.Show(host);
        app.Get<ToastService>().Attach(window);
        Dispatcher.UIThread.RunJobs();
        return (page, window);
    }

    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static List<IconButton> ExportRowButtons(Window window) =>
        window.GetVisualDescendants().OfType<IconButton>().Where(b => b.Name == "ExportRowButton").ToList();

    private static List<string?> Toasts(Window window) =>
        window.GetVisualDescendants().OfType<Toast>().Select(t => t.Message).ToList();

    private static async Task WaitUntilAsync(Func<bool> done)
    {
        for (var i = 0; i < 200 && !done(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(25);
        }

        Assert.True(done());
    }
}
