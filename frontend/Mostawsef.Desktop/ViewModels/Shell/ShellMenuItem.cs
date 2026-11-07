using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Local;

namespace Mostawsef.Desktop.ViewModels.Shell;

/// <summary>A sidebar entry of a portal shell: icon, translated title and the page view model it opens.</summary>
public class ShellMenuItem : ObservableObject
{
    public ShellMenuItem(string iconKey, string titleKey, Type pageType)
    {
        Icon = Icons.Get(iconKey);
        TitleKey = titleKey;
        PageType = pageType;
        LocalizationService.Instance.LanguageChanged += (_, _) => OnPropertyChanged(nameof(Title));
    }

    public Geometry? Icon { get; }

    public string TitleKey { get; }

    public string Title => LocalizationService.Instance.Get(TitleKey);

    public Type PageType { get; }
}
