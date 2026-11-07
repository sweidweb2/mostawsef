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
    private readonly ThemeService _theme;
    private readonly DialogService _dialogs;
    private readonly ToastService _toasts;
    private readonly NavigationService _navigation;

    public GalleryViewModel(ThemeService theme, DialogService dialogs, ToastService toasts, NavigationService navigation)
    {
        _theme = theme;
        _dialogs = dialogs;
        _toasts = toasts;
        _navigation = navigation;

        var today = DateTime.Today;
        Appointments =
        [
            new SchedulerItem { Start = today.AddHours(9), End = today.AddHours(9.5), Title = "أحمد علي", Subtitle = "كشف", Kind = BadgeKind.Primary },
            new SchedulerItem { Start = today.AddHours(9.25), End = today.AddHours(10), Title = "Sara Mahmoud", Subtitle = "Follow-up", Kind = BadgeKind.Success },
            new SchedulerItem { Start = today.AddHours(11), End = today.AddHours(12), Title = "محمد حسن", Subtitle = "استشارة", Kind = BadgeKind.Warning },
            new SchedulerItem { Start = today.AddDays(1).AddHours(13), End = today.AddDays(1).AddHours(14), Title = "Omar Khaled", Subtitle = "Lab review", Kind = BadgeKind.Info },
            new SchedulerItem { Start = today.AddDays(2).AddHours(10), End = today.AddDays(2).AddHours(10.5), Title = "منى يوسف", Subtitle = "ملغي", Kind = BadgeKind.Danger },
        ];
    }

    [ObservableProperty]
    public partial int SelectedSectionIndex { get; set; }

    [ObservableProperty]
    public partial bool IsTableLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisiblePatients))]
    public partial bool IsTableEmpty { get; set; }

    [ObservableProperty]
    public partial bool IsOverlayLoading { get; set; } = true;

    [ObservableProperty]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    public partial int StepIndex { get; set; } = 1;

    [ObservableProperty]
    public partial bool IsDetailsOpen { get; set; }

    [ObservableProperty]
    public partial DateTime? RangeFrom { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial DateTime? RangeTo { get; set; } = DateTime.Today.AddDays(7);

    public IReadOnlyList<string> Steps { get; } = ["Patient", "Visit", "Payment"];

    public IReadOnlyList<string> Drugs { get; } = ["Paracetamol", "Amoxicillin", "Ibuprofen", "Omeprazole", "Metformin", "باراسيتامول"];

    public IReadOnlyList<string> Genders { get; } = ["ذكر / Male", "أنثى / Female"];

    public ObservableCollection<string> ValidationErrors { get; } = ["Patient name is required.", "Phone number must be 11 digits."];

    public ObservableCollection<PatientRow> Patients { get; } =
    [
        new("P-1001", "أحمد علي", 34, "01001234567", "Active", BadgeKind.Success),
        new("P-1002", "Sara Mahmoud", 27, "01111234567", "Waiting", BadgeKind.Warning),
        new("P-1003", "محمد حسن", 52, "01221234567", "Active", BadgeKind.Success),
        new("P-1004", "Omar Khaled", 8, "01551234567", "New", BadgeKind.Info),
        new("P-1005", "منى يوسف", 61, "01009876543", "Unpaid", BadgeKind.Danger),
    ];

    public IEnumerable<PatientRow> VisiblePatients => IsTableEmpty ? [] : Patients;

    public ObservableCollection<SchedulerItem> Appointments { get; }

    [RelayCommand]
    private void Back() => _navigation.NavigateToHome();

    [RelayCommand]
    private void ToggleTheme() => _theme.Toggle();

    [RelayCommand]
    private void ToggleLanguage() => LocalizationService.Instance.Toggle();

    [RelayCommand]
    private void ToggleTableLoading() => IsTableLoading = !IsTableLoading;

    [RelayCommand]
    private void ToggleTableEmpty() => IsTableEmpty = !IsTableEmpty;

    [RelayCommand]
    private void ToggleOverlay() => IsOverlayLoading = !IsOverlayLoading;

    [RelayCommand]
    private void ToggleDetails() => IsDetailsOpen = !IsDetailsOpen;

    [RelayCommand]
    private void ShowToast(string? kind)
    {
        var toastKind = Enum.TryParse<ToastKind>(kind, out var parsed) ? parsed : ToastKind.Info;
        _toasts.Show($"{toastKind} message", LocalizationService.Instance.Get("App.Title"), toastKind);
    }

    [RelayCommand]
    private async Task ShowModalAsync()
    {
        var modal = new Modal
        {
            Title = "Modal",
            Content = new TextBlock { Text = "Any content can go here: forms, details, previews.", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
        };
        modal.Footer = new Button { Content = LocalizationService.Instance.Get("Common.Ok"), Classes = { "primary" }, Command = modal.CloseCommand };
        await _dialogs.ShowAsync(modal);
    }

    [RelayCommand]
    private async Task ShowConfirmAsync()
    {
        var confirmed = await _dialogs.ConfirmAsync(
            "Delete this patient? This cannot be undone.",
            confirmText: LocalizationService.Instance.Get("Common.Delete"),
            isDanger: true);

        _toasts.Show(confirmed ? "Deleted" : "Cancelled", kind: confirmed ? ToastKind.Success : ToastKind.Info);
    }

    [RelayCommand]
    private void Print() => _toasts.Show("Printing is not implemented yet.", kind: ToastKind.Warning);

    [RelayCommand]
    private void FinishWizard() => _toasts.Success("Wizard finished");
}
