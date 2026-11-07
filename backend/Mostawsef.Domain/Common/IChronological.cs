namespace Mostawsef.Domain.Common;

/// <summary>A record listed newest first: by OccurredAt, then Id to order records with the same time.</summary>
public interface IChronological
{
    Guid Id { get; }

    DateTime OccurredAt { get; }
}
