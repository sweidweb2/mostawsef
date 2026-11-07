using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Mostawsef.Desktop.ViewModels;

namespace Mostawsef.Desktop;

/// <summary>
/// Given a view model, returns the corresponding view: ViewModels.X.FooViewModel -> Views.X.FooView.
/// Falls back to base view model types, so several view models can share one view
/// (e.g. AdminShellViewModel and SuperShellViewModel both use Views.Shell.PortalShellView).
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        for (var type = param.GetType(); type is not null && type != typeof(ViewModelBase); type = type.BaseType)
        {
            var viewName = type.FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
            if (Type.GetType(viewName) is { } viewType)
            {
                return (Control)Activator.CreateInstance(viewType)!;
            }
        }

        return new TextBlock { Text = "View not found for " + param.GetType().FullName };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
