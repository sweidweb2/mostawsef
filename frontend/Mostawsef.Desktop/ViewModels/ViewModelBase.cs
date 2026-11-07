using CommunityToolkit.Mvvm.ComponentModel;

namespace Mostawsef.Desktop.ViewModels;

/// <summary>
/// Base of every page view model. Translated text is exposed as resource keys and translated by the view
/// ({l:Tr Key}, {l:TrKey Property}), so view models never subscribe to language changes.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
}
