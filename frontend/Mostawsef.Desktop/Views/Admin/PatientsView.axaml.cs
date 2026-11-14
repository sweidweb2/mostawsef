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
    }

    /// <summary>Tints the rows of patients who have an order that is not paid in full.</summary>
    private void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        e.Row.Classes.Set("unpaid", e.Row.DataContext is PatientDto { IsUnpaid: true });
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
