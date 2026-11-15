using System.Diagnostics;
using Microsoft.Extensions.Options;
using Mostawsef.Application.Modules.Backups;
using Mostawsef.Infrastructure.Services.Backup;

namespace Mostawsef.Api.IntegrationTests.Backups;

/// <summary>
/// Runs the real <see cref="PgDumpRunner"/> against a tiny stand-in for <c>pg_dump</c> (no PostgreSQL tools required).
/// </summary>
public class PgDumpRunnerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly object CompileLock = new();
    private static readonly Dictionary<string, string> CompiledTools = new(StringComparer.Ordinal);

    [Fact]
    public async Task Runs_the_configured_tool_and_keeps_a_non_empty_dump()
    {
        var tool = FakePgDump("success");
        var output = Path.Combine(NewTempFolder(), "out.dump");
        var runner = new PgDumpRunner(
            "Host=127.0.0.1;Port=5432;Username=postgres;Password=secret;Database=mostawsef",
            Options.Create(new ScheduledBackupOptions { PgDumpPath = tool }));

        await runner.RunAsync(output, Ct);

        Assert.True(File.Exists(output));
        Assert.Equal("fake-pg-dump", await File.ReadAllTextAsync(output, Ct));
    }

    [Fact]
    public async Task A_failing_tool_deletes_the_partial_file_and_throws()
    {
        var tool = FakePgDump("fail");
        var output = Path.Combine(NewTempFolder(), "partial.dump");
        var runner = new PgDumpRunner(
            "Host=127.0.0.1;Database=mostawsef",
            Options.Create(new ScheduledBackupOptions { PgDumpPath = tool }));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(output, Ct));

        Assert.Contains("pg_dump failed", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task An_empty_dump_file_is_rejected()
    {
        var tool = FakePgDump("empty");
        var output = Path.Combine(NewTempFolder(), "empty.dump");
        var runner = new PgDumpRunner(
            "Host=127.0.0.1;Database=mostawsef",
            Options.Create(new ScheduledBackupOptions { PgDumpPath = tool }));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(output, Ct));

        Assert.Contains("wrote no dump file", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Publishes a tiny console app once per behavior so <see cref="PgDumpRunner"/> can exec a real binary.</summary>
    private static string FakePgDump(string mode)
    {
        lock (CompileLock)
        {
            if (CompiledTools.TryGetValue(mode, out var existing) && File.Exists(existing))
            {
                return existing;
            }

            var root = Path.Combine(Path.GetTempPath(), "mostawsef-fake-pgdump", mode);
            var projectDir = Path.Combine(root, "src");
            var outputDir = Path.Combine(root, "out");
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(outputDir);

            File.WriteAllText(
                Path.Combine(projectDir, "fake.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <AssemblyName>pg_dump</AssemblyName>
                  </PropertyGroup>
                </Project>
                """);

            File.WriteAllText(Path.Combine(projectDir, "Program.cs"), mode switch
            {
                "fail" => """
                    var file = args.First(a => a.StartsWith("--file=", StringComparison.Ordinal))["--file=".Length..];
                    await File.WriteAllTextAsync(file, "partial");
                    Console.Error.WriteLine("boom");
                    return 1;
                    """,
                "empty" => """
                    var file = args.First(a => a.StartsWith("--file=", StringComparison.Ordinal))["--file=".Length..];
                    await File.WriteAllTextAsync(file, string.Empty);
                    return 0;
                    """,
                _ => """
                    var file = args.First(a => a.StartsWith("--file=", StringComparison.Ordinal))["--file=".Length..];
                    await File.WriteAllTextAsync(file, "fake-pg-dump");
                    return 0;
                    """,
            });

            var publish = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList = { "publish", Path.Combine(projectDir, "fake.csproj"), "-c", "Release", "-o", outputDir, "--nologo", "-v", "q" },
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }) ?? throw new InvalidOperationException("Failed to start dotnet publish for the fake pg_dump.");

            publish.WaitForExit();
            if (publish.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "Could not build fake pg_dump: " + publish.StandardError.ReadToEnd() + publish.StandardOutput.ReadToEnd());
            }

            var exe = Path.Combine(outputDir, OperatingSystem.IsWindows() ? "pg_dump.exe" : "pg_dump");
            if (!File.Exists(exe))
            {
                throw new InvalidOperationException("Fake pg_dump was published but the binary is missing at " + exe);
            }

            CompiledTools[mode] = exe;
            return exe;
        }
    }

    private static string NewTempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "mostawsef-pgdump-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
