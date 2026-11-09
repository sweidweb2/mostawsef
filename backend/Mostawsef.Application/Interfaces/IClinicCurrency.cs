namespace Mostawsef.Application.Interfaces;

/// <summary>The clinic's currency code (one row). Cached until clinic settings are updated.</summary>
public interface IClinicCurrency
{
    Task<string> GetCodeAsync(CancellationToken cancellationToken);

    void Invalidate();
}
