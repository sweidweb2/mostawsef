using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Mostawsef.Api.IntegrationTests.Infrastructure;

/// <summary>Records the SQL of every query the API reads with, for tests that check what is loaded from the database.</summary>
public sealed class SqlRecorder : DbCommandInterceptor
{
    private readonly ConcurrentQueue<string> _queries = new();

    public IReadOnlyCollection<string> Queries => _queries;

    public void Clear() => _queries.Clear();

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        _queries.Enqueue(command.CommandText);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        _queries.Enqueue(command.CommandText);
        return ValueTask.FromResult(result);
    }
}
