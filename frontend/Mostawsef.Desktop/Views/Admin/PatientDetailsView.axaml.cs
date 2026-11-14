using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Orders;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.ViewModels.Admin;

namespace Mostawsef.Desktop.Views.Admin;

public partial class PatientDetailsView : UserControl
{
    public PatientDetailsView()
    {
        InitializeComponent();
        OrderCards.AddHandler(TappedEvent, OnOrderCardTapped);
    }

    /// <summary>A click on an order card opens that order.</summary>
    private void OnOrderCardTapped(object? sender, TappedEventArgs e)
    {
        if (RecordTap.RecordOf(e) is OrderListItemDto order
            && DataContext is PatientDetailsViewModel patient
            && patient.OpenOrderCommand.CanExecute(order))
        {
            patient.OpenOrderCommand.Execute(order);
        }
    }
}
