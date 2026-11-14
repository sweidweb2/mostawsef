using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Mostawsef.Desktop.Components;

/// <summary>
/// A danger confirmation for actions that cannot be undone: the confirm button stays disabled until the user types
/// <see cref="Word"/> exactly (case-sensitive). Closes with true (confirmed) or false/null (cancelled or dismissed).
/// Usually created by DialogService.ConfirmWithWordAsync.
/// </summary>
public class TypeToConfirmDialog : Modal
{
    public TypeToConfirmDialog(string message, string word, string? title = null, string? confirmText = null)
    {
        var loc = App.Localization;
        Word = word;
        Title = title ?? loc.Get("Confirm.Title");
        MaxWidth = 480;

        Input = new TextBox { Name = "ConfirmWordInput", PlaceholderText = word };

        ConfirmButton = new Button
        {
            Name = "ConfirmWordButton",
            Content = confirmText ?? loc.Get("Common.Confirm"),
            Classes = { "danger" },
            Command = CloseCommand,
            CommandParameter = true,
            IsEnabled = false,
        };

        // The property itself, not TextChanged (raised later): the button follows every edit at once.
        Input.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                ConfirmButton.IsEnabled = Matches(Input.Text);
            }
        };
        Input.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter && Matches(Input.Text))
            {
                Close(true);
                e.Handled = true;
            }
        };

        Content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new Alert { Kind = BadgeKind.Danger, Text = message },
                new TextBlock { Text = loc.Format("Confirm.TypeWord", word), TextWrapping = TextWrapping.Wrap },
                Input,
            },
        };

        Footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children =
            {
                new Button
                {
                    Content = loc.Get("Common.Cancel"),
                    Classes = { "secondary" },
                    Command = CloseCommand,
                    CommandParameter = false,
                },
                ConfirmButton,
            },
        };

        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => Input.Focus());
    }

    /// <summary>What the user must type.</summary>
    public string Word { get; }

    public TextBox Input { get; }

    public Button ConfirmButton { get; }

    protected override Type StyleKeyOverride => typeof(Modal);

    private bool Matches(string? text) => string.Equals(text?.Trim(), Word, StringComparison.Ordinal);
}
