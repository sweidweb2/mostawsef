using System;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;

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

    void Success(string message, string? title = null) => Show(message, title, ToastKind.Success);

    void Error(string message, string? title = null) => Show(message, title, ToastKind.Error);
}

public class ToastService : IToastService
{
    private WindowNotificationManager? _manager;

    public void Attach(TopLevel topLevel) =>
        _manager = new WindowNotificationManager(topLevel)
        {
            Position = NotificationPosition.BottomRight,
            MaxItems = 4,
        };

    public void Show(string message, string? title = null, ToastKind kind = ToastKind.Info, TimeSpan? duration = null)
    {
        var type = kind switch
        {
            ToastKind.Success => NotificationType.Success,
            ToastKind.Warning => NotificationType.Warning,
            ToastKind.Error => NotificationType.Error,
            _ => NotificationType.Information,
        };

        _manager?.Show(new Notification(title, message, type, duration ?? TimeSpan.FromSeconds(4)));
    }
}
