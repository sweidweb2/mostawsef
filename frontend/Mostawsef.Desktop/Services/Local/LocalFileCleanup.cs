using System;
using System.IO;
using System.Linq;
using Mostawsef.Desktop.Services.Printing;

namespace Mostawsef.Desktop.Services.Local;

/// <summary>
/// Keeps the files the app leaves on the PC from piling up: old log files and old printed PDFs.
/// Best effort: a file that is locked or cannot be deleted is skipped and tried again next time.
/// </summary>
public static class LocalFileCleanup
{
    /// <summary>Error and performance logs older than this are deleted.</summary>
    public static readonly TimeSpan LogRetention = TimeSpan.FromDays(30);

    /// <summary>Printed PDFs hold patient data, so they do not stay longer than this.</summary>
    public static readonly TimeSpan PrintRetention = TimeSpan.FromDays(1);

    /// <summary>Sweeps the logs and the print folder. Run it once at startup, off the UI thread.</summary>
    public static void Run(string logDirectory)
    {
        DeleteOlderThan(logDirectory, "*.log", LogRetention);
        DeletePrints();
    }

    /// <summary>Deletes printed PDFs past <see cref="PrintRetention"/>. Cheap: also used before each print and at exit.</summary>
    public static void DeletePrints()
    {
        DeleteOlderThan(PrintService.PrintFolder, "*.pdf", PrintRetention);
        DeleteOlderThan(PrintService.PrintFolder, "*.html", PrintRetention);
    }

    /// <summary>Deletes files matching <paramref name="pattern"/> last written longer than <paramref name="maxAge"/> ago.</summary>
    /// <returns>How many files were deleted.</returns>
    public static int DeleteOlderThan(string directory, string pattern, TimeSpan maxAge)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var cutoff = DateTime.Now - maxAge;
        var deleted = 0;
        try
        {
            foreach (var file in new DirectoryInfo(directory).EnumerateFiles(pattern).Where(f => f.LastWriteTime < cutoff))
            {
                try
                {
                    file.Delete();
                    deleted++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Still open in a viewer, or not ours to delete: try again next time.
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The folder disappeared or cannot be listed: nothing to clean.
        }

        return deleted;
    }
}
