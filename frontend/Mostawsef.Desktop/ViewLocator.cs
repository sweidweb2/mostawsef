using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Mostawsef.Desktop.ViewModels;

namespace Mostawsef.Desktop;

/// <summary>Builds the view of a view model from <see cref="AppRoutes"/>.</summary>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param) =>
        param is null
            ? null
            : AppRoutes.Table.CreateView(param.GetType())
              ?? new TextBlock { Text = "View not found for " + param.GetType().FullName };

    public bool Match(object? data) => data is ViewModelBase;
}
