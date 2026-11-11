using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Tests.Infrastructure;
using static Mostawsef.Desktop.Tests.Infrastructure.TestServices;

namespace Mostawsef.Desktop.Tests.Views;

[Collection(UiTestCollection.Name)]
public class ErrorStateTests : UiTestBase
{
    [AvaloniaFact]
    public void Error_state_shows_the_default_translated_texts_and_no_retry_without_a_command()
    {
        using var app = new TestServices();
        var window = app.Show(new ErrorState());

        Assert.Contains("Something went wrong", Texts(window));
        Assert.Contains("We couldn't load this data. Please try again.", Texts(window));
        Assert.False(RetryButton(window).IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void Clicking_retry_runs_the_retry_command()
    {
        using var app = new TestServices();
        var runs = 0;
        var window = app.Show(new ErrorState { RetryCommand = new RelayCommand(() => runs++) });

        var retry = RetryButton(window);
        Assert.True(retry.IsEffectivelyVisible);
        Assert.Contains("Try again", Texts(window));

        Click(window, retry);

        Assert.Equal(1, runs);
    }

    [AvaloniaFact]
    public void Data_table_shows_the_error_instead_of_empty_and_hides_it_while_loading()
    {
        using var app = new TestServices();
        var table = new DataTable { IsEmpty = true, HasError = true, ErrorTitle = "Cannot reach the server" };
        var window = app.Show(table);
        var error = window.GetVisualDescendants().OfType<ErrorState>().Single();
        var empty = window.GetVisualDescendants().OfType<EmptyState>().Single();

        Assert.True(error.IsEffectivelyVisible);
        Assert.False(empty.IsEffectivelyVisible);
        Assert.Equal("Cannot reach the server", error.Title);

        table.IsLoading = true;
        Assert.False(error.IsEffectivelyVisible);

        table.IsLoading = false;
        table.HasError = false;
        Assert.False(error.IsEffectivelyVisible);
        Assert.True(empty.IsEffectivelyVisible);
    }

    private static Button RetryButton(Window window) =>
        window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PART_Retry");

    private static void Click(Window window, Button button)
    {
        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }
}
