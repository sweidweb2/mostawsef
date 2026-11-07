using System;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Mostawsef.Desktop.Components;

/// <summary>Yes / no Modal. Closes with true (confirm) or false (cancel). Usually created by DialogService.ConfirmAsync.</summary>
public class ConfirmDialog : Modal
{
    public ConfirmDialog(string message, string? title = null, string? confirmText = null, string? cancelText = null, bool isDanger = false)
    {
        var loc = App.Localization;
        Title = title ?? loc.Get("Confirm.Title");
        MaxWidth = 440;
        Content = new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap };

        var cancel = new Button
        {
            Content = cancelText ?? loc.Get("Common.Cancel"),
            Classes = { "secondary" },
            Command = CloseCommand,
            CommandParameter = false,
        };

        var confirm = new Button
        {
            Content = confirmText ?? loc.Get("Common.Confirm"),
            Classes = { isDanger ? "danger" : "primary" },
            Command = CloseCommand,
            CommandParameter = true,
        };

        Footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { cancel, confirm },
        };
    }

    protected override Type StyleKeyOverride => typeof(Modal);
}
