using Mostawsef.Domain.Common;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Domain.Entities;

/// <summary>
/// A person the clinic serves. Deleted patients are kept (soft delete) so their past orders stay readable.
/// The caller passes the search name and phones already normalized with Contracts SearchText.
/// </summary>
public class Patient : SoftDeletableEntity
{
    public const int FullNameMaxLength = 100;
    public const int PhoneMaxLength = 30;
    public const int NotesMaxLength = 2000;

    private Patient()
    {
    }

    public Patient(
        string fullName, string searchName, string phone, string? guardianPhone, string? notes, bool isInsured, Gender gender, DateOnly? dateOfBirth) =>
        Update(fullName, searchName, phone, guardianPhone, notes, isInsured, gender, dateOfBirth);

    public string FullName { get; private set; } = string.Empty;

    /// <summary>FullName normalized for search.</summary>
    public string SearchName { get; private set; } = string.Empty;

    /// <summary>Latin digits, single spaces, trimmed: "٠١٠  ١٢٣" and "010 123" are stored and searched the same way. Not unique: siblings share a guardian's phone.</summary>
    public string Phone { get; private set; } = string.Empty;

    public string? GuardianPhone { get; private set; }

    public string? Notes { get; private set; }

    public bool IsInsured { get; private set; }

    public Gender Gender { get; private set; }

    public DateOnly? DateOfBirth { get; private set; }

    public void Update(
        string fullName, string searchName, string phone, string? guardianPhone, string? notes, bool isInsured, Gender gender, DateOnly? dateOfBirth)
    {
        FullName = fullName.Trim();
        SearchName = searchName;
        Phone = phone;
        GuardianPhone = string.IsNullOrEmpty(guardianPhone) ? null : guardianPhone;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        IsInsured = isInsured;
        Gender = gender;
        DateOfBirth = dateOfBirth;
    }
}
