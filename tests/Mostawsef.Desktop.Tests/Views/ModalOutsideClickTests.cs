using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Admin;

namespace Mostawsef.Desktop.Tests.Views;

/// <summary>Clicking the dark area around a dialog acts like its X button.</summary>
[Collection(UiTestCollection.Name)]
public class ModalOutsideClickTests : UiTestBase
{
    private static readonly Point Outside = new(10, 10);

    [AvaloniaFact]
    public void Clicking_outside_an_unchanged_form_closes_it()
    {
        using var app = new TestServices();
        var (window, dialogs) = Host(app);
        var shown = dialogs.ShowFormAsync(NewForm(app), "New lab test");
        Dispatcher.UIThread.RunJobs();

        Click(window, Outside);

        Assert.True(shown.IsCompleted);
        Assert.False(shown.Result);
        Assert.Empty(window.GetVisualDescendants().OfType<Modal>());
    }

    [AvaloniaFact]
    public void Clicking_inside_or_dragging_from_inside_to_outside_keeps_the_dialog_open()
    {
        using var app = new TestServices();
        var (window, dialogs) = Host(app);
        var shown = dialogs.ShowFormAsync(NewForm(app), "New lab test");
        Dispatcher.UIThread.RunJobs();
        var inside = CenterOf(window.GetVisualDescendants().OfType<Modal>().Single(), window);

        Click(window, inside);
        window.MouseDown(inside, MouseButton.Left);
        window.MouseUp(Outside, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.False(shown.IsCompleted);
        Assert.Single(window.GetVisualDescendants().OfType<Modal>());
    }

    [AvaloniaFact]
    public void Clicking_outside_a_changed_form_asks_first_and_clicking_outside_the_question_keeps_editing()
    {
        using var app = new TestServices();
        var (window, dialogs) = Host(app);
        var form = NewForm(app);
        var shown = dialogs.ShowFormAsync(form, "New lab test");
        Dispatcher.UIThread.RunJobs();
        form.Name = "Half typed";

        Click(window, Outside);
        var question = window.GetVisualDescendants().OfType<ConfirmDialog>().Single();
        Click(window, Outside);

        Assert.DoesNotContain(question, window.GetVisualDescendants().OfType<Modal>());
        Assert.Equal("Half typed", Assert.IsType<LabTestFormViewModel>(window.GetVisualDescendants().OfType<Modal>().Single().Content).Name);
        Assert.False(shown.IsCompleted);

        Click(window, Outside);
        window.GetVisualDescendants().OfType<ConfirmDialog>().Single().Close(true);
        Dispatcher.UIThread.RunJobs();

        Assert.True(shown.IsCompleted);
        Assert.Empty(window.GetVisualDescendants().OfType<Modal>());
    }

    [AvaloniaFact]
    public void A_dialog_without_a_close_button_ignores_clicks_outside()
    {
        using var app = new TestServices();
        var (window, dialogs) = Host(app);
        var shown = dialogs.ShowAsync(new Modal { Title = "Required choice", ShowCloseButton = false, Content = new TextBlock { Text = "..." } });
        Dispatcher.UIThread.RunJobs();

        Click(window, Outside);

        Assert.False(shown.IsCompleted);
        Assert.Single(window.GetVisualDescendants().OfType<Modal>());
    }

    private static (Window Window, DialogService Dialogs) Host(TestServices app)
    {
        var host = new DialogHost { Content = new TextBlock { Text = "Page" } };
        var dialogs = app.Get<DialogService>();
        dialogs.Attach(host);
        var window = app.Show(host);
        Dispatcher.UIThread.RunJobs();
        return (window, dialogs);
    }

    private static LabTestFormViewModel NewForm(TestServices app) =>
        new(app.Get<LabTestsApi>(), app.Get<IDialogService>(), app.Localization);

    private static void Click(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static Point CenterOf(Control control, Window window) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
}
