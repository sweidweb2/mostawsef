using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.Tests.Infrastructure;

namespace Mostawsef.Desktop.Tests.Views;

[Collection(UiTestCollection.Name)]
public class ToastTests : UiTestBase
{
    [AvaloniaFact]
    public void Toast_uses_the_app_design_with_icon_message_and_close_button_and_no_empty_title()
    {
        using var app = new TestServices();
        var (toasts, window) = Attach(app);

        toasts.Success("Record deleted.");
        Dispatcher.UIThread.RunJobs();

        var card = window.GetVisualDescendants().OfType<NotificationCard>().Single();
        var parts = card.GetVisualDescendants().OfType<Border>().Select(b => b.Name).ToList();
        Assert.Contains("PART_Accent", parts);
        Assert.DoesNotContain(card.GetVisualDescendants().OfType<Panel>(), p => p.Name == "PART_HeaderBar");

        var toast = card.GetVisualDescendants().OfType<Toast>().Single();
        Assert.Equal(BadgeKind.Success, toast.Kind);
        var texts = toast.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        Assert.Equal(["Record deleted."], texts);
        Assert.Single(toast.GetVisualDescendants().OfType<Button>(), b => NotificationCard.GetCloseOnClick(b));
    }

    [AvaloniaFact]
    public void Each_kind_gets_its_own_color_and_title_is_shown_when_given()
    {
        using var app = new TestServices();
        var (toasts, window) = Attach(app);

        toasts.Show("Could not reach the server.", "Connection", ToastKind.Error);
        Dispatcher.UIThread.RunJobs();

        var card = window.GetVisualDescendants().OfType<NotificationCard>().Single();
        Assert.Contains(":error", card.Classes);
        var toast = card.GetVisualDescendants().OfType<Toast>().Single();
        Assert.Equal(BadgeKind.Danger, toast.Kind);
        Assert.Contains("Connection", toast.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text));
    }

    [AvaloniaFact]
    public async Task Toasts_can_be_raised_from_a_background_thread()
    {
        using var app = new TestServices();
        var (toasts, window) = Attach(app);

        await Task.Run(() => toasts.Error("Background failure"));
        Dispatcher.UIThread.RunJobs();

        Assert.Single(window.GetVisualDescendants().OfType<Toast>(), t => t.Message == "Background failure");
    }

    private static (ToastService Toasts, Window Window) Attach(TestServices app)
    {
        var window = app.Show(new Border());
        var toasts = app.Get<ToastService>();
        toasts.Attach(window);
        Dispatcher.UIThread.RunJobs();
        return (toasts, window);
    }
}
