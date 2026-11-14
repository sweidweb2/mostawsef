using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Ui;

namespace Mostawsef.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        App.Services.GetRequiredService<DialogService>().Attach(Host);
        App.Services.GetRequiredService<ToastService>().Attach(this);
        FitRestoredSizeToScreen();

        // JIT and first-time XAML work for the main views happen while the user types the password, not after sign-in.
        var session = App.Services.GetRequiredService<SessionService>();
        Opened += (_, _) => _ = ViewWarmUp.RunAsync(WarmUpLayer, () => !session.IsAuthenticated);
    }

    private void FitRestoredSizeToScreen()
    {
        if (Screens.Primary is not { } screen)
        {
            return;
        }

        var workArea = screen.WorkingArea.Size.ToSize(screen.Scaling);
        var size = WindowSizing.FitToWorkArea(new Size(Width, Height), workArea);
        Width = size.Width;
        Height = size.Height;
    }
}
