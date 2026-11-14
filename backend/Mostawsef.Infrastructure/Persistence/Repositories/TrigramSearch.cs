using Mostawsef.Contracts.Common;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

/// <summary>
/// Builds a LIKE '%term%' pattern for a lowercased SearchText column so the GIN trigram index can serve it.
/// The term's LIKE wildcards are escaped, so "%" and "_" match themselves.
/// </summary>
internal static class TrigramSearch
{
    public const string EscapeCharacter = "\\";

    public static string? ContainsPattern(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var normalized = SearchText.Normalize(search);
        if (normalized.Length == 0)
        {
            return null;
        }

        var escaped = EscapeLike(normalized);
        return string.Create(escaped.Length + 2, escaped, static (span, term) =>
        {
            span[0] = '%';
            term.AsSpan().CopyTo(span[1..]);
            span[^1] = '%';
        });
    }

    private static string EscapeLike(string term)
    {
        var extra = 0;
        foreach (var c in term)
        {
            if (c is '\\' or '%' or '_')
            {
                extra++;
            }
        }

        if (extra == 0)
        {
            return term;
        }

        return string.Create(term.Length + extra, term, static (span, value) =>
        {
            var i = 0;
            foreach (var c in value)
            {
                if (c is '\\' or '%' or '_')
                {
                    span[i++] = '\\';
                }

                span[i++] = c;
            }
        });
    }
}
