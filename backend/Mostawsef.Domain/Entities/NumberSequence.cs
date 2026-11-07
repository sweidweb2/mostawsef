namespace Mostawsef.Domain.Entities;

/// <summary>A named counter for readable document numbers (patient files, invoices). One row per name.</summary>
public class NumberSequence
{
    public const int NameMaxLength = 50;

    private NumberSequence()
    {
    }

    public NumberSequence(string name, long nextValue)
    {
        Name = name;
        NextValue = nextValue;
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>The number the next caller gets.</summary>
    public long NextValue { get; private set; }
}
