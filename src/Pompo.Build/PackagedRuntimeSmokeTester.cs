using System.Diagnostics;
using System.Text.Json;
using Pompo.Core.Project;
using Pompo.VisualScripting.Runtime;

namespace Pompo.Build;

public sealed class PackagedRuntimeSmokeTester
{
    public async Task<IReadOnlyList<BuildDiagnostic>> RunAsync(
        string outputDirectory,
        string runtimeDirectory,
        IReadOnlyList<string> compiledGraphs,
        IReadOnlyList<string>? locales = null,
        CancellationToken cancellationToken = default)
    {
        var executable = ResolveRuntimeExecutable(runtimeDirectory);
        if (executable is null)
        {
            return
            [
                new BuildDiagnostic(
                    "BUILD006",
                    $"Packaged runtime executable was not found in '{runtimeDirectory}'.",
                    runtimeDirectory)
            ];
        }

        var smokeLocales = locales is null || locales.Count == 0
            ? [(string?)null]
            : locales
                .Where(locale => !string.IsNullOrWhiteSpace(locale))
                .Distinct(StringComparer.Ordinal)
                .Select(locale => (string?)locale)
                .DefaultIfEmpty(null)
                .ToArray();

        foreach (var graphFile in compiledGraphs)
        {
            var graphPath = Path.Combine(outputDirectory, "Data", graphFile);
            foreach (var locale in smokeLocales)
            {
                var diagnostic = await RunGraphAsync(executable, graphPath, locale, cancellationToken).ConfigureAwait(false);
                if (diagnostic is not null)
                {
                    return [diagnostic];
                }
            }
        }

        return [];
    }

    private static async Task<BuildDiagnostic?> RunGraphAsync(
        string executable,
        string graphPath,
        string? locale,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executable)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };

        process.StartInfo.ArgumentList.Add("--play-ir");
        process.StartInfo.ArgumentList.Add(graphPath);
        if (!string.IsNullOrWhiteSpace(locale))
        {
            process.StartInfo.ArgumentList.Add("--locale");
            process.StartInfo.ArgumentList.Add(locale);
        }

        process.StartInfo.ArgumentList.Add("--json-trace");

        try
        {
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                return new BuildDiagnostic(
                    "BUILD007",
                    $"Runtime smoke test failed for '{FormatGraphName(graphPath, locale)}' with exit code {process.ExitCode}: {CombineOutput(stdout, stderr)}",
                    graphPath);
            }

            var trace = JsonSerializer.Deserialize<RuntimeTraceResult>(stdout, ProjectFileService.CreateJsonOptions());
            if (trace?.Completed != true)
            {
                return new BuildDiagnostic(
                    "BUILD008",
                    $"Runtime smoke test for '{FormatGraphName(graphPath, locale)}' did not produce a completed JSON trace.",
                    graphPath);
            }

            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return new BuildDiagnostic(
                "BUILD009",
                $"Runtime smoke test timed out for '{FormatGraphName(graphPath, locale)}'.",
                graphPath);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException)
        {
            return new BuildDiagnostic(
                "BUILD010",
                $"Runtime smoke test could not run for '{FormatGraphName(graphPath, locale)}': {ex.Message}",
                graphPath);
        }
    }

    private static string? ResolveRuntimeExecutable(string runtimeDirectory)
    {
        var executableName = OperatingSystem.IsWindows()
            ? "Pompo.Runtime.Fna.exe"
            : "Pompo.Runtime.Fna";
        var candidate = Path.Combine(runtimeDirectory, executableName);
        return File.Exists(candidate) ? candidate : null;
    }

    private static string CombineOutput(string stdout, string stderr)
    {
        var message = string.Join(
            Environment.NewLine,
            new[] { stdout.Trim(), stderr.Trim() }.Where(text => !string.IsNullOrWhiteSpace(text)));
        return string.IsNullOrWhiteSpace(message) ? "<no output>" : message;
    }

    private static string FormatGraphName(string graphPath, string? locale)
    {
        var name = Path.GetFileName(graphPath);
        return string.IsNullOrWhiteSpace(locale) ? name : $"{name} (locale {locale})";
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
