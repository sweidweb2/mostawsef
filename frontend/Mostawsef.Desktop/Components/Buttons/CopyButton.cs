using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Mostawsef.Desktop.Services.Local;

namespace Mostawsef.Desktop.Components;

/// <summary>
/// Small icon button that copies <see cref="Text"/> to the clipboard; the icon turns into a check mark for a moment.
/// Hidden while Text is empty. Combine with size classes, e.g. Classes="sm".
/// </summary>
public class CopyButton : IconButton
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<CopyButton, string?>(nameof(Text));

    public static readonly TimeSpan ConfirmationTime = TimeSpan.FromSeconds(1.5);

    private IDisposable? _resetIcon;

    public CopyButton()
    {
        Classes.Add("ghost");
        Icon = Icons.Get("IconCopy");
        IsVisible = false;
        Bind(ToolTip.TipProperty, TrExtension.CreateBinding("Common.Copy"));
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            IsVisible = !string.IsNullOrEmpty(Text);
        }
    }

    protected override async void OnClick()
    {
        base.OnClick();
        if (string.IsNullOrEmpty(Text) || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        try
        {
            await clipboard.SetTextAsync(Text);
        }
        catch (Exception)
        {
            // Clipboard can be locked by another process (RDP, managers); leave the icon alone.
            return;
        }

        Icon = Icons.Get("IconCheck");
        _resetIcon?.Dispose();
        _resetIcon = DispatcherTimer.RunOnce(() => Icon = Icons.Get("IconCopy"), ConfirmationTime);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _resetIcon?.Dispose();
        Icon = Icons.Get("IconCopy");
        base.OnDetachedFromVisualTree(e);
    }
}
