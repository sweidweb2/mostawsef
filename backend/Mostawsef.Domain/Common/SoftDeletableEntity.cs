namespace Mostawsef.Domain.Common;

/// <summary>
/// Entities that are hidden instead of removed. Deleting one marks it deleted (with who and when),
/// and every query skips it unless it explicitly asks for deleted rows.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }

    DateTime? DeletedAt { get; }

    string? DeletedBy { get; }
}

public abstract class SoftDeletableEntity : Entity, ISoftDeletable
{
    public bool IsDeleted { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }
}
