using System.Diagnostics;
using Microsoft.Extensions.Options;
using Mostawsef.Application.Interfaces;
using Mostawsef.Application.Modules.Backups;
using Npgsql;

namespace Mostawsef.Infrastructure.Services.Backup;

/// <summary>Runs the local <c>pg_dump</c> tool against the API's PostgreSQL connection string.</summary>
public sealed class PgDumpRunner(string connectionString, IOptions<ScheduledBackupOptions> options) : IDatabaseDumpRunner
{
    public async Task RunAsync(string outputFilePath, CancellationToken cancellationToken)
    {
        var cs = new NpgsqlConnectionStringBuilder(connectionString);
        var pgDump = ResolvePgDumpPath(options.Value.PgDumpPath)
            ?? throw new InvalidOperationException(
                "pg_dump was not found. Install PostgreSQL client tools on this PC, or set ScheduledBackup:PgDumpPath.");

        var start = new ProcessStartInfo
        {
            FileName = pgDump,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("--format=custom");
        start.ArgumentList.Add("--file=" + outputFilePath);
        start.ArgumentList.Add("--no-password");
        if (!string.IsNullOrWhiteSpace(cs.Host))
        {
            start.ArgumentList.Add("--host=" + cs.Host);
        }

        if (cs.Port > 0)
        {
            start.ArgumentList.Add("--port=" + cs.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(cs.Username))
        {
            start.ArgumentList.Add("--username=" + cs.Username);
        }

        start.ArgumentList.Add("--dbname=" + (string.IsNullOrWhiteSpace(cs.Database) ? "postgres" : cs.Database));
        if (!string.IsNullOrEmpty(cs.Password))
        {
            start.Environment["PGPASSWORD"] = cs.Password;
        }

        using var process = new Process { StartInfo = start };
        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start pg_dump.");
        }

        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort: the dump process may already have exited.
            }

            throw;
        }

        var stderr = await stderrTask.ConfigureAwait(false);
        _ = await stdoutTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            if (File.Exists(outputFilePath))
            {
                File.Delete(outputFilePath);
            }

            var detail = string.IsNullOrWhiteSpace(stderr) ? $"exit {process.ExitCode}" : Truncate(stderr.Trim(), 2000);
            throw new InvalidOperationException("pg_dump failed: " + detail);
        }

        if (!File.Exists(outputFilePath) || new FileInfo(outputFilePath).Length == 0)
        {
            throw new InvalidOperationException("pg_dump finished but wrote no dump file.");
        }
    }

    private static string? ResolvePgDumpPath(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        var fromPath = FindOnPath("pg_dump") ?? FindOnPath("pg_dump.exe");
        if (fromPath is not null)
        {
            return fromPath;
        }

        if (OperatingSystem.IsWindows())
        {
            foreach (var root in new[]
                     {
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                     })
            {
                if (string.IsNullOrWhiteSpace(root))
                {
                    continue;
                }

                var postgresRoot = Path.Combine(root, "PostgreSQL");
                if (!Directory.Exists(postgresRoot))
                {
                    continue;
                }

                foreach (var bin in Directory.EnumerateDirectories(postgresRoot)
                             .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase)
                             .Select(d => Path.Combine(d, "bin", "pg_dump.exe")))
                {
                    if (File.Exists(bin))
                    {
                        return bin;
                    }
                }
            }
        }

        return null;
    }

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
