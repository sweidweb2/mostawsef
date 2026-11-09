using Avalonia.Controls;
using Mostawsef.Desktop.ViewModels.Admin;

namespace Mostawsef.Desktop.Views.Admin;

public partial class NewOrderView : UserControl
{
    public NewOrderView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => WirePatientFilter();
        WirePatientFilter();
    }

    /// <summary>ItemFilter is a delegate, so it is wired here instead of through a binding.</summary>
    private void WirePatientFilter()
    {
        if (this.FindControl<AutoCompleteBox>("PatientSearchBox") is { } box)
        {
            box.ItemFilter = NewOrderViewModel.MatchesPatient;
        }
    }
}
