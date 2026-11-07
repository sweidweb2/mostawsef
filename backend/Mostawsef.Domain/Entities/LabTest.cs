using Mostawsef.Domain.Common;

namespace Mostawsef.Domain.Entities;

/// <summary>A test the clinic offers. Prices are minor units of the clinic currency; orders copy them when created.</summary>
public class LabTest : Entity
{
    public const int CodeMaxLength = 20;
    public const int NameMaxLength = 150;

    private LabTest()
    {
    }

    public LabTest(string code, string name, long price, long labCost) => Update(code, name, price, labCost, isActive: true);

    /// <summary>Trimmed, upper-case. Unique.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>Name normalized with <see cref="SearchText"/>. Unique, so two tests cannot differ only in spelling.</summary>
    public string SearchName { get; private set; } = string.Empty;

    /// <summary>What the clinic charges the patient.</summary>
    public long Price { get; private set; }

    /// <summary>What the lab charges the clinic.</summary>
    public long LabCost { get; private set; }

    /// <summary>Inactive tests stay in the list but are not offered for new orders.</summary>
    public bool IsActive { get; private set; } = true;

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    public void Update(string code, string name, long price, long labCost, bool isActive)
    {
        Code = NormalizeCode(code);
        Name = name.Trim();
        SearchName = SearchText.Normalize(Name);
        Price = price;
        LabCost = labCost;
        IsActive = isActive;
    }
}
