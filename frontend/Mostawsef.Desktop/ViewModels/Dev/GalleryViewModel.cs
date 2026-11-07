using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.Services.Ui;

namespace Mostawsef.Desktop.ViewModels.Dev;

public record PatientRow(string FileNumber, string Name, int Age, string Phone, string Status, BadgeKind StatusKind);

/// <summary>Development-only showcase of the shared components. Sample data only.</summary>
public partial class GalleryViewModel : ViewModelBase
{
    private readonly DialogService _dialogs;
    private readonly ToastService _toasts;
    private readonly NavigationService _navigation;
    private readonly LocalizationService _localization;

    public GalleryViewModel(DialogService dialogs, ToastService toasts, NavigationService navigation, LocalizationService localization)
    {
        _dialogs = dialogs;
        _toasts = toasts;
        _navigation = navigation;
        _localization = localization;
    }

    [ObservableProperty]
    public partial int SelectedSectionIndex { get; set; }

    [ObservableProperty]
    public partial bool IsTableLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisiblePatients))]
    public partial bool IsTableEmpty { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisiblePatients))]
    public partial bool IsTableError { get; set; }

    [ObservableProperty]
    public partial bool IsOverlayLoading { get; set; } = true;

    [ObservableProperty]
    public partial int CurrentPage { get; set; } = 1;

    public IReadOnlyList<string> Drugs { get; } = ["Paracetamol", "Amoxicillin", "Ibuprofen", "Omeprazole", "Metformin", "باراسيتامول"];

    public IReadOnlyList<string> Genders { get; } = ["ذكر / Male", "أنثى / Female"];

    public ObservableCollection<PatientRow> Patients { get; } =
    [
        new("P-1001", "أحمد علي", 34, "01001234567", "Active", BadgeKind.Success),
        new("P-1002", "Sara Mahmoud", 27, "01111234567", "Waiting", BadgeKind.Warning),
        new("P-1003", "محمد حسن", 52, "01221234567", "Active", BadgeKind.Success),
        new("P-1004", "Omar Khaled", 8, "01551234567", "New", BadgeKind.Info),
        new("P-1005", "منى يوسف", 61, "01009876543", "Unpaid", BadgeKind.Danger),
    ];

    public IEnumerable<PatientRow> VisiblePatients => IsTableEmpty || IsTableError ? [] : Patients;

    [RelayCommand]
    private void Back() => _navigation.NavigateToHome();

    [RelayCommand]
    private void ToggleTableLoading() => IsTableLoading = !IsTableLoading;

    [RelayCommand]
    private void ToggleTableEmpty() => IsTableEmpty = !IsTableEmpty;

    [RelayCommand]
    private void ToggleTableError() => IsTableError = !IsTableError;

    [RelayCommand]
    private void ToggleOverlay() => IsOverlayLoading = !IsOverlayLoading;

    [RelayCommand]
    private void ShowToast(string? kind)
    {
        var toastKind = Enum.TryParse<ToastKind>(kind, out var parsed) ? parsed : ToastKind.Info;
        _toasts.Show($"{toastKind} message", _localization.Get("App.Title"), toastKind);
    }

    [RelayCommand]
    private async Task ShowModalAsync()
    {
        var modal = new Modal
        {
            Title = "Modal",
            Content = new TextBlock { Text = "Any content can go here: forms, details, previews.", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
        };
        modal.Footer = new Button { Content = _localization.Get("Common.Ok"), Classes = { "primary" }, Command = modal.CloseCommand };
        await _dialogs.ShowAsync(modal);
    }

    [RelayCommand]
    private async Task ShowConfirmAsync()
    {
        var confirmed = await _dialogs.ConfirmAsync(
            "Delete this patient? This cannot be undone.",
            confirmText: _localization.Get("Common.Delete"),
            isDanger: true);

        _toasts.Show(confirmed ? "Deleted" : "Cancelled", kind: confirmed ? ToastKind.Success : ToastKind.Info);
    }
}
