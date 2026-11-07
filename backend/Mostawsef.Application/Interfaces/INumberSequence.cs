namespace Mostawsef.Application.Interfaces;

/// <summary>
/// Hands out 1, 2, 3... per sequence name (e.g. "patients"), safely across PCs and parallel requests.
/// The number is taken in its own transaction: if the save that uses it fails, that number is skipped (gaps are possible).
/// Format it with Domain DocumentNumber.Format.
/// </summary>
public interface INumberSequence
{
    Task<long> NextAsync(string name, CancellationToken cancellationToken);
}
