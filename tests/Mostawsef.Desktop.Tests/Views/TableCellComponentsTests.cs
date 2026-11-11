using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Tests.Infrastructure;

namespace Mostawsef.Desktop.Tests.Views;

/// <summary>Row numbers, colored initials and copy buttons used inside table cells.</summary>
[Collection(UiTestCollection.Name)]
public class TableCellComponentsTests : UiTestBase
{
    private static readonly string[] SampleNames =
        ["Rabi3 kasir", "Jawad ezzeddine", "zein baydon", "Abdallah abbas", "hasan rahhal", "hussein fayad"];

    [AvaloniaFact]
    public void Row_numbers_continue_from_the_tables_first_row_number_and_follow_changes()
    {
        using var app = new TestServices();
        var grid = new DataGrid
        {
            ItemsSource = new List<string> { "a", "b", "c" },
            Columns = { new DataGridTemplateColumn { CellTemplate = new FuncDataTemplate<string>((_, _) => new RowNumber()) } },
        };
        var table = new DataTable { FirstRowNumber = 41, Content = grid };
        var window = app.Show(table);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("a=41 b=42 c=43", Numbers(window));

        table.FirstRowNumber = 1;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("a=1 b=2 c=3", Numbers(window));

        grid.ItemsSource = new List<string> { "y", "x" };
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("x=2 y=1", Numbers(window));

        var live = new System.Collections.ObjectModel.ObservableCollection<string> { "p", "q" };
        grid.ItemsSource = live;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("p=1 q=2", Numbers(window));
        live.Insert(0, "o");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("o=1 p=2 q=3", Numbers(window));
    }

    [AvaloniaFact]
    public void A_colorful_avatar_shows_one_letter_and_one_stable_tone_per_name()
    {
        using var app = new TestServices();
        var avatar = new Avatar { DisplayName = "Mona Adel", MaxInitials = 1, IsColorful = true };
        var plain = new Avatar { DisplayName = "Mona Adel" };
        app.Show(new StackPanel { Children = { avatar, plain } });

        Assert.Equal("M", avatar.Initials);
        Assert.Equal("MA", plain.Initials);
        Assert.Equal([$":tone{Avatar.ToneOf("Mona Adel")}"], Tones(avatar));
        Assert.Empty(Tones(plain));
        Assert.Equal(Avatar.ToneOf("Mona Adel"), Avatar.ToneOf("  mona adel "));

        avatar.DisplayName = "عمر خالد";
        Assert.Equal("ع", avatar.Initials);
        Assert.Single(Tones(avatar));
        var tones = SampleNames.Select(Avatar.ToneOf).Distinct().Count();
        Assert.True(tones >= 3, $"only {tones} tones for six names");
    }

    [AvaloniaFact]
    public async Task The_copy_button_puts_its_text_on_the_clipboard_and_confirms_with_a_check_mark()
    {
        using var app = new TestServices();
        var button = new CopyButton { Text = "010 1234 5678" };
        var empty = new CopyButton { Text = "" };
        var window = app.Show(new StackPanel { Children = { button, empty } });
        Dispatcher.UIThread.RunJobs();

        Assert.True(button.IsVisible);
        Assert.False(empty.IsVisible);
        Assert.Equal("Copy", ToolTip.GetTip(button));
        Assert.Equal(Icons.Get("IconCopy"), button.Icon);

        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("010 1234 5678", await window.Clipboard!.TryGetTextAsync());
        Assert.Equal(Icons.Get("IconCheck"), button.Icon);
    }

    /// <summary>"item=number" for every visible row, sorted by item (rows are recycled, so their visual order is not the display order).</summary>
    private static string Numbers(Window window) => string.Join(' ', window.GetVisualDescendants().OfType<RowNumber>()
        .Where(n => n.IsEffectivelyVisible)
        .Select(n => $"{n.DataContext}={n.Text}")
        .Order(StringComparer.Ordinal));

    private static List<string> Tones(Avatar avatar) =>
        Enumerable.Range(0, Avatar.ToneCount).Select(i => $":tone{i}").Where(avatar.Classes.Contains).ToList();
}
