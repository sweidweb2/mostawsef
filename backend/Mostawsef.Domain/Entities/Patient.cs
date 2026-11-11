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
    public const int SearchTextMaxLength = FullNameMaxLength + PhoneMaxLength + PhoneMaxLength + 2;

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

    /// <summary>SearchName, phone and guardian phone joined for list search.</summary>
    public string SearchText { get; private set; } = string.Empty;

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
        SearchText = BuildSearchText(searchName, phone, GuardianPhone);
    }

    private static string BuildSearchText(string searchName, string phone, string? guardianPhone)
    {
        var length = 0;
        if (!string.IsNullOrEmpty(searchName))
        {
            length = searchName.Length;
        }

        if (!string.IsNullOrEmpty(phone))
        {
            length += length > 0 ? 1 + phone.Length : phone.Length;
        }

        if (!string.IsNullOrEmpty(guardianPhone))
        {
            length += length > 0 ? 1 + guardianPhone.Length : guardianPhone.Length;
        }

        if (length == 0)
        {
            return string.Empty;
        }

        return string.Create(length, (searchName, phone, guardianPhone), static (span, state) =>
        {
            var i = 0;
            Append(ref i, span, state.searchName);
            Append(ref i, span, state.phone);
            Append(ref i, span, state.guardianPhone);
        });

        static void Append(ref int i, Span<char> span, string? part)
        {
            if (string.IsNullOrEmpty(part))
            {
                return;
            }

            if (i > 0)
            {
                span[i++] = '\n';
            }

            part.AsSpan().CopyTo(span[i..]);
            i += part.Length;
        }
    }
}
