using System;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using Mostawsef.Desktop.Components;

namespace Mostawsef.Desktop.Services.Ui;

public enum ToastKind
{
    Info,
    Success,
    Warning,
    Error,
}

public interface IToastService
{
    void Show(string message, string? title = null, ToastKind kind = ToastKind.Info, TimeSpan? duration = null);

    void Success(string message, string? title = null);

    void Error(string message, string? title = null);
}

/// <summary>Toast notifications (bottom corner of the window). Safe to call from any thread.</summary>
public class ToastService : IToastService
{
    private WindowNotificationManager? _manager;

    public void Attach(TopLevel topLevel) =>
        _manager = new WindowNotificationManager(topLevel)
        {
            Position = NotificationPosition.BottomRight,
            MaxItems = 4,
            Margin = new Avalonia.Thickness(0, 0, 8, 16),
        };

    public void Show(string message, string? title = null, ToastKind kind = ToastKind.Info, TimeSpan? duration = null)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Show(message, title, kind, duration));
            return;
        }

        if (_manager is null)
        {
            return;
        }

        var (type, badge) = kind switch
        {
            ToastKind.Success => (NotificationType.Success, BadgeKind.Success),
            ToastKind.Warning => (NotificationType.Warning, BadgeKind.Warning),
            ToastKind.Error => (NotificationType.Error, BadgeKind.Danger),
            _ => (NotificationType.Information, BadgeKind.Info),
        };

        // Errors stay a little longer so they can be read.
        var expiration = duration ?? TimeSpan.FromSeconds(kind == ToastKind.Error ? 6 : 4);
        _manager.Show(new Toast { Title = title, Message = message, Kind = badge }, type, expiration);
    }

    public void Success(string message, string? title = null) => Show(message, title, ToastKind.Success);

    public void Error(string message, string? title = null) => Show(message, title, ToastKind.Error);
}
