using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Patients;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Admin;

/// <summary>Create a patient (no <paramref name="existing"/>) or edit one.</summary>
public partial class PatientFormViewModel : FormViewModel
{
    private readonly PatientsApi _patients;
    private readonly PatientDto? _existing;

    public PatientFormViewModel(PatientsApi patients, IDialogService dialogs, LocalizationService localization, PatientDto? existing = null)
        : base(dialogs, localization)
    {
        _patients = patients;
        _existing = existing;

        if (existing is not null)
        {
            FullName = existing.FullName;
            Phone = existing.Phone;
            GuardianPhone = existing.GuardianPhone ?? string.Empty;
            Notes = existing.Notes ?? string.Empty;
            IsInsured = existing.IsInsured;
            Gender = existing.Gender;
            DateOfBirth = existing.DateOfBirth;
        }

        MarkClean();
    }

    public IReadOnlyList<string> GenderOptions => Genders.All;

    /// <summary>Range offered by the date picker; the same range the API accepts.</summary>
    public DateTime MinDateOfBirth { get; } = PatientRules.MinDateOfBirth.ToDateTime(TimeOnly.MinValue);

    public DateTime MaxDateOfBirth { get; } = DateTime.Today;

    [ObservableProperty]
    public partial string FullName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Phone { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GuardianPhone { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Notes { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsInsured { get; set; }

    [ObservableProperty]
    public partial string Gender { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateOnly? DateOfBirth { get; set; }

    /// <summary>The new patient as typed (trimmed), e.g. for an order that creates the patient.</summary>
    public CreatePatientRequest ToCreateRequest() => (CreatePatientRequest)BuildRequest();

    /// <summary>The fields the API requires are filled in (they may still be invalid).</summary>
    public bool HasRequiredFields =>
        !string.IsNullOrWhiteSpace(FullName) && !string.IsNullOrWhiteSpace(Phone) && !string.IsNullOrEmpty(Gender);

    /// <summary>Clears the create form so a reused New order page starts empty.</summary>
    public void Reset()
    {
        FullName = string.Empty;
        Phone = string.Empty;
        GuardianPhone = string.Empty;
        Notes = string.Empty;
        IsInsured = false;
        Gender = string.Empty;
        DateOfBirth = null;
        Errors.Clear();
        FormErrorKey = null;
        MarkClean();
    }

    protected override object BuildRequest()
    {
        var request = new CreatePatientRequest
        {
            FullName = FullName.Trim(),
            Phone = Phone.Trim(),
            GuardianPhone = Blank(GuardianPhone),
            Notes = Blank(Notes),
            IsInsured = IsInsured,
            Gender = Gender,
            DateOfBirth = DateOfBirth,
        };

        return _existing is null
            ? request
            : new UpdatePatientRequest
            {
                FullName = request.FullName,
                Phone = request.Phone,
                GuardianPhone = request.GuardianPhone,
                Notes = request.Notes,
                IsInsured = request.IsInsured,
                Gender = request.Gender,
                DateOfBirth = request.DateOfBirth,
                Version = _existing.Version,
            };
    }

    protected override void ValidateMore(FormErrors errors)
    {
        if (DateOfBirth is { } date
            && (date < PatientRules.MinDateOfBirth || date > DateOnly.FromDateTime(MaxDateOfBirth))
            && errors[nameof(DateOfBirth)] is null)
        {
            errors.Set(nameof(DateOfBirth), ErrorCodes.Invalid);
        }
    }

    protected override async Task<IApiResult> SubmitAsync(object request, CancellationToken cancellationToken) => request switch
    {
        UpdatePatientRequest update => await _patients.UpdatePatientAsync(_existing!.Id, update, cancellationToken),
        _ => await _patients.CreatePatientAsync((CreatePatientRequest)request, cancellationToken),
    };

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
