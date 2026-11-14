using System.Net;
using System.Net.Http.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Maintenance;
using Mostawsef.Contracts.Settings;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Super;

namespace Mostawsef.Desktop.Tests.Settings;

[Collection(UiTestCollection.Name)]
public class SystemSettingsPageTests : UiTestBase
{
    private const string SettingsPath = "/api/system-settings";
    private const string ResetPath = "/api/system/reset-data";

    private static readonly SystemSettingsDto Current = new(@"D:\Backups", false, 2, DateTime.UtcNow, "super@test.local");

    [AvaloniaFact]
    public void Only_super_users_can_open_the_settings_page()
    {
        using var app = new TestServices();
        Assert.False(app.Navigation.CanAccess(typeof(SystemSettingsViewModel)));

        app.SignInAs(Roles.Admin);
        Assert.False(app.Navigation.CanAccess(typeof(SystemSettingsViewModel)));

        app.SignInAs(Roles.Super);
        Assert.True(app.Navigation.CanAccess(typeof(SystemSettingsViewModel)));
    }

    [AvaloniaFact]
    public async Task The_page_shows_the_backup_folder_and_maintenance_mode_in_both_languages()
    {
        using var app = SuperWithSettings(Current);
        var (_, window) = await ShowPage(app);

        var values = Details(window);
        Assert.Equal(@"D:\Backups", values["Backup folder"]);
        Assert.Equal("Off", values["Maintenance mode"]);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Alert>(), a => a.IsVisible);

        app.Localization.SetLanguage(AppLanguage.Arabic);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("متوقف", Details(window)["وضع الصيانة"]);
    }

    [AvaloniaFact]
    public async Task Maintenance_mode_on_shows_a_warning()
    {
        using var app = SuperWithSettings(Current with { MaintenanceMode = true });
        var (_, window) = await ShowPage(app);

        Assert.Equal("On", Details(window)["Maintenance mode"]);
        var alert = window.GetVisualDescendants().OfType<Alert>().Single(a => a.IsVisible);
        Assert.Equal("Maintenance mode is on: only Super users can sign in.", alert.Text);
    }

    [AvaloniaFact]
    public async Task A_failed_load_shows_the_error_state()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        app.Api.FailWithNetworkError();

        var (page, window) = await ShowPage(app);

        Assert.True(page.HasLoadError);
        Assert.False(page.HasSettings);
        Assert.True(window.GetVisualDescendants().OfType<ErrorState>().Single().IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task Editing_sends_the_loaded_version_and_shows_the_saved_values()
    {
        using var app = SuperWithSettings(Current);
        UpdateSystemSettingsRequest? sent = null;
        app.Api.On(HttpMethod.Put, SettingsPath, request =>
        {
            sent = request.Content!.ReadFromJsonAsync<UpdateSystemSettingsRequest>().GetAwaiter().GetResult();
            return FakeApiHandler.Json(new SystemSettingsDto(sent!.BackupPath, sent.MaintenanceMode, 3, DateTime.UtcNow, "super@test.local"));
        });
        var (page, window) = await ShowPage(app);

        var edit = page.EditCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        var form = (SystemSettingsFormViewModel)window.GetVisualDescendants().OfType<Modal>().Single().Content!;
        form.BackupPath = @"  E:\Clinic\Backups ";
        form.MaintenanceMode = true;
        await form.SaveCommand.ExecuteAsync(null);
        await edit;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Current.Version, sent!.Version);
        Assert.Equal(@"E:\Clinic\Backups", sent.BackupPath);
        Assert.True(sent.MaintenanceMode);
        Assert.Equal(@"E:\Clinic\Backups", Details(window)["Backup folder"]);
        Assert.Equal("On", Details(window)["Maintenance mode"]);
        Assert.Contains("Settings saved.", window.GetVisualDescendants().OfType<Toast>().Select(t => t.Message));
    }

    [AvaloniaFact]
    public async Task A_too_long_backup_path_shows_a_field_error_without_calling_the_api()
    {
        using var app = SuperWithSettings(Current);
        var (page, window) = await ShowPage(app);

        _ = page.EditCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        var form = (SystemSettingsFormViewModel)window.GetVisualDescendants().OfType<Modal>().Single().Content!;
        form.BackupPath = new string('a', SystemSettingsRules.BackupPathMaxLength + 1);
        await form.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal($"Must be at most {SystemSettingsRules.BackupPathMaxLength} characters.", FieldError(window, "Backup folder"));
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Put, SettingsPath));
    }

    [AvaloniaFact]
    public async Task A_conflicting_edit_shows_the_conflict_message()
    {
        using var app = SuperWithSettings(Current);
        app.Api.On(HttpMethod.Put, SettingsPath, _ => FakeApiHandler.Problem(HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict));
        var (page, window) = await ShowPage(app);

        _ = page.EditCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        var form = (SystemSettingsFormViewModel)window.GetVisualDescendants().OfType<Modal>().Single().Content!;
        form.MaintenanceMode = true;
        await form.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        var alert = window.GetVisualDescendants().OfType<Alert>().Single(a => a.IsVisible);
        Assert.StartsWith("Someone else changed this record", alert.Text);
        Assert.False(page.Settings!.MaintenanceMode);
    }

    [AvaloniaFact]
    public async Task Reset_stays_disabled_until_the_confirmation_word_is_typed_then_resets_and_reports_what_went()
    {
        using var app = SuperWithSettings(Current);
        ResetDataRequest? sent = null;
        app.Api.On(HttpMethod.Post, ResetPath, request =>
        {
            sent = request.Content!.ReadFromJsonAsync<ResetDataRequest>().GetAwaiter().GetResult();
            return FakeApiHandler.Json(new ResetDataResultDto(12, 30, 41, 8, 200, 5));
        });
        var (page, window) = await ShowPage(app);

        var reset = page.ResetDataCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        var dialog = window.GetVisualDescendants().OfType<TypeToConfirmDialog>().Single();
        Assert.Contains("To confirm, type RESET below.", TestServices.Texts(window));
        Assert.False(dialog.ConfirmButton.IsEnabled);

        dialog.Input.Text = "reset";
        Assert.False(dialog.ConfirmButton.IsEnabled);
        dialog.Input.Text = DataResetRules.ConfirmationWord;
        Assert.True(dialog.ConfirmButton.IsEnabled);

        dialog.ConfirmButton.Command!.Execute(dialog.ConfirmButton.CommandParameter);
        await reset;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(DataResetRules.ConfirmationWord, sent!.Confirmation);
        Assert.Contains(
            "Database reset: 12 patients, 30 orders, 8 lab tests and 205 log records deleted.",
            window.GetVisualDescendants().OfType<Toast>().Select(t => t.Message));
    }

    [AvaloniaFact]
    public async Task Cancelling_the_reset_dialog_deletes_nothing()
    {
        using var app = SuperWithSettings(Current);
        var (page, window) = await ShowPage(app);

        var reset = page.ResetDataCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        var dialog = window.GetVisualDescendants().OfType<TypeToConfirmDialog>().Single();
        dialog.Input.Text = DataResetRules.ConfirmationWord;
        dialog.Close(false);
        await reset;

        Assert.Empty(app.Api.RequestsTo(HttpMethod.Post, ResetPath));
    }

    private static TestServices SuperWithSettings(SystemSettingsDto settings)
    {
        var app = new TestServices();
        app.SignInAs(Roles.Super);
        app.Api.OnGet(SettingsPath, settings);
        return app;
    }

    private static async Task<(SystemSettingsViewModel Page, Window Window)> ShowPage(TestServices app)
    {
        var page = app.Get<SystemSettingsViewModel>();
        await page.ActivateAsync();
        var host = new DialogHost { Content = new ContentControl { Content = page } };
        app.Get<DialogService>().Attach(host);
        var window = app.Show(host);
        app.Get<ToastService>().Attach(window);
        Dispatcher.UIThread.RunJobs();
        return (page, window);
    }

    private static string? FieldError(Window window, string label) =>
        window.GetVisualDescendants().OfType<FormField>().Single(f => f.Label == label && f.IsEffectivelyVisible).Error;

    private static Dictionary<string, string> Details(Window window) =>
        window.GetVisualDescendants().OfType<DetailsItem>().ToDictionary(i => i.Label!, i => i.DisplayValue);
}
