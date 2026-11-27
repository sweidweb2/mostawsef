using Mostawsef.Application.Common;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Patients;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Application.Modules.Patients;

/// <summary>Turns a patient request into a <see cref="Patient"/>; shared by the patients and orders modules.</summary>
internal static class PatientInput
{
    public static Patient Create(CreatePatientRequest request) =>
        new(
            request.FullName, SearchText.Normalize(request.FullName), SearchText.Normalize(request.Phone),
            request.Notes, ParseGender(request.Gender), request.DateOfBirth);

    public static void Apply(Patient patient, CreatePatientRequest request) =>
        patient.Update(
            request.FullName, SearchText.Normalize(request.FullName), SearchText.Normalize(request.Phone),
            request.Notes, ParseGender(request.Gender), request.DateOfBirth);

    /// <summary>Between PatientRules.MinDateOfBirth and today (server local date); else 400 "invalid" on <paramref name="field"/>.</summary>
    public static void EnsureValidDateOfBirth(DateOnly? dateOfBirth, TimeProvider time, string field)
    {
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        if (dateOfBirth is { } date && (date < PatientRules.MinDateOfBirth || date > today))
        {
            throw AppException.Validation(field, ErrorCodes.Invalid);
        }
    }

    private static Gender ParseGender(string gender) =>
        Enum.TryParse<Gender>(gender, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw AppException.Validation(nameof(CreatePatientRequest.Gender), ErrorCodes.Invalid);
}
