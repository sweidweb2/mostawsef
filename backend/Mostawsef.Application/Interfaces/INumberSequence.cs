namespace Mostawsef.Application.Interfaces;

/// <summary>
/// Hands out 1, 2, 3... per sequence name (e.g. "orders"), safely across PCs and parallel requests.
/// When called inside the caller's DbContext transaction (order create), a failed save rolls the number back with it.
/// Format it with Domain DocumentNumber.Format.
/// </summary>
public interface INumberSequence
{
    Task<long> NextAsync(string name, CancellationToken cancellationToken);
}
