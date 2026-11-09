using System.Net;
using System.Net.Http.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Users;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Super;
using static Mostawsef.Desktop.Tests.Infrastructure.TestServices;

namespace Mostawsef.Desktop.Tests.Users;

public class UsersPageTests
{
    private const string UsersPath = "/api/users";

    private static readonly UserListItemDto Sara =
        new(Guid.NewGuid(), "sara@test.local", "Sara Mahmoud", Roles.Admin, true, DateTime.UtcNow, DateTime.UtcNow);

    private static readonly UserListItemDto Omar =
        new(Guid.NewGuid(), "omar@test.local", "Omar Khaled", Roles.Super, false, null, DateTime.UtcNow);

    private static readonly UserDetailsDto SaraDetails =
        new(Sara.Id, Sara.Username, Sara.FullName, Sara.Role, true, null, DateTime.UtcNow, "super@test.local", null, null, 3);

    [AvaloniaFact]
    public void Only_super_users_can_open_the_users_page()
    {
        using var app = new TestServices();
        Assert.False(app.Navigation.CanAccess(typeof(UsersViewModel)));

        app.SignInAs(Roles.Admin);
        Assert.False(app.Navigation.CanAccess(typeof(UsersViewModel)));

        app.SignInAs(Roles.Super);
        Assert.True(app.Navigation.CanAccess(typeof(UsersViewModel)));
    }

    [AvaloniaFact]
    public async Task The_list_shows_names_translated_roles_and_status()
    {
        using var app = SuperWithUsers(Sara, Omar);

        var (page, window) = await ShowPage(app);

        Assert.Equal(2, page.Items.Count);
        var texts = Texts(window);
        Assert.Contains("Sara Mahmoud", texts);
        Assert.Contains("omar@test.local", texts);
        Assert.Contains("Super admin", window.GetVisualDescendants().OfType<Badge>().Select(b => b.Text));
        Assert.Contains("Inactive", window.GetVisualDescendants().OfType<Badge>().Where(b => b.IsEffectivelyVisible).Select(b => b.Text));
        Assert.Contains("2 users", texts);
        var numbers = window.GetVisualDescendants().OfType<RowNumber>().ToDictionary(n => ((UserListItemDto)n.DataContext!).Id, n => n.Text);
        Assert.Equal(("1", "2"), (numbers[Sara.Id], numbers[Omar.Id]));
    }

    [AvaloniaFact]
    public async Task Page_flips_skip_the_filtered_total()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        app.Api.On(HttpMethod.Get, UsersPath, request =>
        {
            var includeTotal = !request.RequestUri!.Query.Contains("includeTotal=false", StringComparison.Ordinal);
            return FakeApiHandler.Json(new PagedResult<UserListItemDto>(
                [includeTotal ? Sara : Omar],
                includeTotal ? 1 : 2,
                20,
                includeTotal ? 40 : PageQuery.TotalNotComputed));
        });

        var page = app.Get<UsersViewModel>();
        await page.ActivateAsync();
        Assert.Contains("includeTotal=true", app.Api.RequestsTo(HttpMethod.Get, UsersPath).Last().RequestUri!.Query, StringComparison.Ordinal);
        Assert.Equal(40, page.TotalCount);

        page.CurrentPage = 2;
        await WaitUntil(() => page.CurrentPage == 2 && !page.IsLoading);

        Assert.Contains("includeTotal=false", app.Api.RequestsTo(HttpMethod.Get, UsersPath).Last().RequestUri!.Query, StringComparison.Ordinal);
        Assert.Equal(40, page.TotalCount);
        Assert.Equal(Omar.Id, Assert.Single(page.Items).Id);
    }

    [AvaloniaFact]
    public async Task Saving_an_empty_new_user_shows_field_errors_without_calling_the_api()
    {
        using var app = SuperWithUsers(Sara);
        var (page, window) = await ShowPage(app);

        var form = OpenAddForm(page, window);
        await form.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ErrorCodes.Required, form.Errors[nameof(form.FullName)]);
        Assert.Equal(ErrorCodes.Required, form.Errors[nameof(form.Username)]);
        Assert.Equal(ErrorCodes.Required, form.Errors[nameof(form.Password)]);
        Assert.Equal("This field is required.", FieldError(window, "Full name"));
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Post, UsersPath));
    }

    [AvaloniaFact]
    public async Task Rules_with_a_limit_show_the_limit_and_follow_the_language()
    {
        using var app = SuperWithUsers(Sara);
        var (page, window) = await ShowPage(app);
        var form = OpenAddForm(page, window);
        form.FullName = "Sara";
        form.Username = "sara@test.local";
        form.Password = "short";

        await form.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Must be at least 8 characters.", FieldError(window, "Password"));
        app.Localization.SetLanguage(AppLanguage.Arabic);
        Assert.Equal("يجب ألا يقل عن 8 أحرف.", FieldError(window, "كلمة المرور"));
    }

    [AvaloniaFact]
    public async Task Editing_a_field_clears_its_error()
    {
        using var app = SuperWithUsers(Sara);
        var (page, window) = await ShowPage(app);
        var form = OpenAddForm(page, window);
        await form.SaveCommand.ExecuteAsync(null);

        form.FullName = "Sara";

        Assert.Null(form.Errors[nameof(form.FullName)]);
        Assert.NotNull(form.Errors[nameof(form.Username)]);
    }

    [AvaloniaFact]
    public async Task A_valid_new_user_is_sent_then_the_form_closes_and_the_list_reloads()
    {
        using var app = SuperWithUsers(Sara);
        CreateUserRequest? sent = null;
        app.Api.On(HttpMethod.Post, UsersPath, request =>
        {
            sent = request.Content!.ReadFromJsonAsync<CreateUserRequest>().GetAwaiter().GetResult();
            return FakeApiHandler.Json(SaraDetails);
        });
        var (page, window) = await ShowPage(app);
        var listRequests = app.Api.RequestsTo(HttpMethod.Get, UsersPath).Count();

        var add = page.AddCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        var form = CurrentForm<UserFormViewModel>(window);
        form.FullName = "  Sara Mahmoud ";
        form.Username = " sara@test.local ";
        form.Password = "Welcome#123";
        form.Role = Roles.Super;
        await form.SaveCommand.ExecuteAsync(null);
        await add;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new CreateUserRequest { FullName = "Sara Mahmoud", Username = "sara@test.local", Password = "Welcome#123", Role = Roles.Super }, sent);
        Assert.Empty(window.GetVisualDescendants().OfType<Modal>());
        Assert.True(app.Api.RequestsTo(HttpMethod.Get, UsersPath).Count() > listRequests);
        Assert.Contains("User saved.", Toasts(window));
    }

    [AvaloniaFact]
    public async Task A_field_error_from_the_server_shows_next_to_that_field_and_keeps_the_form_open()
    {
        using var app = SuperWithUsers(Sara);
        app.Api.On(HttpMethod.Post, UsersPath, _ =>
            FakeApiHandler.Problem(HttpStatusCode.Conflict, ErrorCodes.UsernameTaken, ("Username", ErrorCodes.UsernameTaken)));
        var (page, window) = await ShowPage(app);
        var form = OpenAddForm(page, window);
        form.FullName = "Sara";
        form.Username = "sara@test.local";
        form.Password = "Welcome#123";

        await form.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.StartsWith("This username is already used", FieldError(window, "Username"));
        Assert.False(form.HasFormError);
        Assert.Single(window.GetVisualDescendants().OfType<Modal>());
    }

    [AvaloniaFact]
    public async Task A_stale_edit_shows_the_conflict_message_at_the_top_of_the_form()
    {
        using var app = SuperWithUsers(Sara);
        app.Api.OnGet($"{UsersPath}/{Sara.Id}", SaraDetails);
        UpdateUserRequest? sent = null;
        app.Api.On(HttpMethod.Put, $"{UsersPath}/{Sara.Id}", request =>
        {
            sent = request.Content!.ReadFromJsonAsync<UpdateUserRequest>().GetAwaiter().GetResult();
            return FakeApiHandler.Problem(HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict);
        });
        var (page, window) = await ShowPage(app);

        _ = page.EditCommand.ExecuteAsync(Sara);
        Dispatcher.UIThread.RunJobs();
        var form = CurrentForm<UserFormViewModel>(window);
        var alert = window.GetVisualDescendants().OfType<Alert>().Single();
        Assert.False(form.IsNew);
        Assert.False(alert.IsVisible);

        form.FullName = "Sara M.";
        await form.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(SaraDetails.Version, sent!.Version);
        Assert.True(alert.IsVisible);
        Assert.StartsWith("Someone else changed this record", alert.Text);
    }

    [AvaloniaFact]
    public async Task Saving_while_the_server_is_down_says_so_at_the_top_of_the_form()
    {
        using var app = SuperWithUsers(Sara);
        var (page, window) = await ShowPage(app);
        var form = OpenAddForm(page, window);
        form.FullName = "Sara";
        form.Username = "sara@test.local";
        form.Password = "Welcome#123";
        app.Api.On(HttpMethod.Post, UsersPath, _ => throw new HttpRequestException("Connection refused"));

        await form.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Login.ServerUnreachable", form.FormErrorKey);
    }

    [AvaloniaFact]
    public async Task Closing_a_changed_form_asks_first_and_keep_editing_leaves_it_open()
    {
        using var app = SuperWithUsers(Sara);
        var (page, window) = await ShowPage(app);
        var add = page.AddCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        var modal = window.GetVisualDescendants().OfType<Modal>().Single();
        var form = CurrentForm<UserFormViewModel>(window);

        form.FullName = "Half typed";
        Assert.True(form.IsDirty);
        var dismiss = modal.DismissAsync();
        Dispatcher.UIThread.RunJobs();
        Discard(window).Close(false);
        await dismiss;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(modal, window.GetVisualDescendants().OfType<Modal>());

        dismiss = modal.DismissAsync();
        Dispatcher.UIThread.RunJobs();
        Discard(window).Close(true);
        await dismiss;
        await add;

        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Modal>(), m => m == modal);
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Post, UsersPath));
    }

    [AvaloniaFact]
    public async Task An_unchanged_form_closes_without_asking()
    {
        using var app = SuperWithUsers(Sara);
        var (page, window) = await ShowPage(app);
        var add = page.AddCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        var form = CurrentForm<UserFormViewModel>(window);

        await form.CancelCommand.ExecuteAsync(null);
        await add;

        Assert.False(form.IsDirty);
        Assert.Empty(window.GetVisualDescendants().OfType<ConfirmDialog>());
        Assert.Empty(window.GetVisualDescendants().OfType<Modal>());
    }

    [AvaloniaFact]
    public async Task Reset_password_needs_a_matching_confirmation()
    {
        using var app = SuperWithUsers(Sara);
        app.Api.On(HttpMethod.Post, $"{UsersPath}/{Sara.Id}/reset-password", HttpStatusCode.NoContent);
        var (page, window) = await ShowPage(app);
        var reset = page.ResetPasswordCommand.ExecuteAsync(Sara);
        Dispatcher.UIThread.RunJobs();
        var form = CurrentForm<ResetPasswordFormViewModel>(window);

        form.NewPassword = "Fresh#Pass99";
        form.ConfirmPassword = "Fresh#Pass98";
        await form.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("The passwords do not match.", FieldError(window, "Confirm password"));
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Post, $"{UsersPath}/{Sara.Id}/reset-password"));

        form.ConfirmPassword = "Fresh#Pass99";
        await form.SaveCommand.ExecuteAsync(null);
        await reset;
        Dispatcher.UIThread.RunJobs();

        Assert.Single(app.Api.RequestsTo(HttpMethod.Post, $"{UsersPath}/{Sara.Id}/reset-password"));
        Assert.Contains("Password reset.", Toasts(window));
    }

    [AvaloniaFact]
    public async Task Deleting_your_own_account_shows_why_it_was_refused()
    {
        using var app = SuperWithUsers(Sara);
        app.Api.On(HttpMethod.Delete, $"{UsersPath}/{Sara.Id}", _ => FakeApiHandler.Problem(HttpStatusCode.Conflict, ErrorCodes.OwnAccount));
        var (page, window) = await ShowPage(app);

        var delete = page.DeleteCommand.ExecuteAsync(Sara);
        Dispatcher.UIThread.RunJobs();
        window.GetVisualDescendants().OfType<ConfirmDialog>().Single().Close(true);
        await delete;
        Dispatcher.UIThread.RunJobs();

        Assert.Single(app.Api.RequestsTo(HttpMethod.Delete, $"{UsersPath}/{Sara.Id}"));
        Assert.Contains("You can't do this to your own account.", Toasts(window));
    }

    private static TestServices SuperWithUsers(params UserListItemDto[] users)
    {
        var app = new TestServices();
        app.SignInAs(Roles.Super);
        app.Api.OnGet(UsersPath, new PagedResult<UserListItemDto>(users, 1, 20, users.Length));
        return app;
    }

    /// <summary>The page inside a DialogHost (attached to the DialogService) with toasts attached to the window.</summary>
    private static async Task<(UsersViewModel Page, Window Window)> ShowPage(TestServices app)
    {
        var page = app.Get<UsersViewModel>();
        await page.ActivateAsync();
        var host = new DialogHost { Content = new ContentControl { Content = page } };
        app.Get<DialogService>().Attach(host);
        var window = app.Show(host);
        app.Get<ToastService>().Attach(window);
        Dispatcher.UIThread.RunJobs();
        return (page, window);
    }

    private static UserFormViewModel OpenAddForm(UsersViewModel page, Window window)
    {
        _ = page.AddCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        return CurrentForm<UserFormViewModel>(window);
    }

    private static T CurrentForm<T>(Window window) =>
        (T)window.GetVisualDescendants().OfType<Modal>().Last().Content!;

    private static ConfirmDialog Discard(Window window) =>
        window.GetVisualDescendants().OfType<ConfirmDialog>().Single();

    /// <summary>The error text under the FormField with this label.</summary>
    private static string? FieldError(Window window, string label) =>
        window.GetVisualDescendants().OfType<FormField>().Single(f => f.Label == label && f.IsEffectivelyVisible).Error;

    private static List<string?> Toasts(Window window) =>
        window.GetVisualDescendants().OfType<Toast>().Select(t => t.Message).ToList();

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }
}
