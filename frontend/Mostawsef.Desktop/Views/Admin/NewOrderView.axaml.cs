using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Mostawsef.Contracts.Patients;
using Mostawsef.Desktop.ViewModels.Admin;

namespace Mostawsef.Desktop.Views.Admin;

public partial class NewOrderView : UserControl
{
    public NewOrderView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// AsyncPopulator is a delegate, so it is wired here. Avalonia only opens the drop-down after
    /// PopulateComplete during a user-driven populate; binding ItemsSource later leaves it closed.
    /// </summary>
    private void PatientSearchBox_OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not AutoCompleteBox box || box.AsyncPopulator is not null)
        {
            return;
        }

        box.AsyncPopulator = async (search, cancellationToken) =>
        {
            if (box.DataContext is not NewOrderViewModel order)
            {
                return Array.Empty<PatientDto>();
            }

            return await order.SearchPatientsAsync(search, cancellationToken);
        };
    }
}
