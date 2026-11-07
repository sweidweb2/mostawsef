using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Desktop.Services.Ui;

namespace Mostawsef.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        App.Services.GetRequiredService<DialogService>().Attach(Host);
        App.Services.GetRequiredService<ToastService>().Attach(this);
    }
}
