using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Patients;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.ViewModels.Admin;

namespace Mostawsef.Desktop.Views.Admin;

public partial class PatientsView : UserControl
{
    public PatientsView()
    {
        InitializeComponent();
        // Tunnelling: the DataGrid itself uses Enter to move to the next row, which would change the selection first.
        PatientsGrid.AddHandler(KeyDownEvent, OnGridKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    /// <summary>Tints the rows of patients who have an order that is not paid in full.</summary>
    private void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        e.Row.Classes.Set("unpaid", e.Row.DataContext is PatientDto { IsUnpaid: true });
    }

    /// <summary>Enter on the selected table row opens that patient (the keyboard way to do what a click does).</summary>
    private void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is DataGrid { SelectedItem: PatientDto patient } && DataContext is PatientsViewModel patients
            && patients.OpenCommand.CanExecute(patient))
        {
            e.Handled = true;
            patients.OpenCommand.Execute(patient);
        }
    }

    /// <summary>A click on a patient (a table row or a card) opens their details; the edit, delete and copy buttons keep their own click.</summary>
    private void OnRecordTapped(object? sender, TappedEventArgs e)
    {
        if (RecordTap.RecordOf(e) is PatientDto patient && DataContext is PatientsViewModel patients && patients.OpenCommand.CanExecute(patient))
        {
            patients.OpenCommand.Execute(patient);
        }
    }
}
