namespace Mostawsef.Application.Interfaces;

/// <summary>Writes a restorable PostgreSQL dump to a file (e.g. via <c>pg_dump</c>).</summary>
public interface IDatabaseDumpRunner
{
    Task RunAsync(string outputFilePath, CancellationToken cancellationToken);
}
