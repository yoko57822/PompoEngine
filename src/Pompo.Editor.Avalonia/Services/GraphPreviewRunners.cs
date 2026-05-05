using System.Diagnostics;
using System.Text.Json;
using Pompo.Core;
using Pompo.Core.Localization;
using Pompo.Core.Project;
using Pompo.VisualScripting;
using Pompo.VisualScripting.Runtime;

namespace Pompo.Editor.Avalonia.Services;

public interface IGraphPreviewRunner
{
    Task<RuntimeTraceResult> RunAsync(
        PompoGraphIR ir,
        PompoProjectDocument? project = null,
        string? locale = null,
        IReadOnlyDictionary<string, PompoGraphIR>? graphLibrary = null,
        CancellationToken cancellationToken = default);
}

public sealed class InProcessGraphPreviewRunner : IGraphPreviewRunner
{
    public Task<RuntimeTraceResult> RunAsync(
        PompoGraphIR ir,
        PompoProjectDocument? project = null,
        string? locale = null,
        IReadOnlyDictionary<string, PompoGraphIR>? graphLibrary = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new RuntimeTraceRunner().Run(
            ir,
            localizer: GraphPreviewLocalizerFactory.Create(project, locale),
            graphLibrary: graphLibrary));
    }
}

public sealed class RuntimeProcessGraphPreviewRunner : IGraphPreviewRunner
{
    public async Task<RuntimeTraceResult> RunAsync(
        PompoGraphIR ir,
        PompoProjectDocument? project = null,
        string? locale = null,
        IReadOnlyDictionary<string, PompoGraphIR>? graphLibrary = null,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var processCancellationToken = timeout.Token;
        var runtimeProject = ResolveRuntimeProjectPath()
            ?? throw new InvalidOperationException("Could not find Pompo.Runtime.Fna.csproj for isolated preview.");
        var tempDirectory = Path.Combine(Path.GetTempPath(), "pompo-preview", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var irPath = Path.Combine(tempDirectory, $"{ir.GraphId}.pompo-ir.json");

        try
        {
            await AtomicFileWriter.WriteJsonAsync(
                irPath,
                ir,
                ProjectFileService.CreateJsonOptions(),
                processCancellationToken).ConfigureAwait(false);
            foreach (var sibling in graphLibrary?.Values ?? [])
            {
                if (string.Equals(sibling.GraphId, ir.GraphId, StringComparison.Ordinal))
                {
                    continue;
                }

                await AtomicFileWriter.WriteJsonAsync(
                    Path.Combine(tempDirectory, $"{sibling.GraphId}.pompo-ir.json"),
                    sibling,
                    ProjectFileService.CreateJsonOptions(),
                    processCancellationToken).ConfigureAwait(false);
            }

            if (project is not null && !string.IsNullOrWhiteSpace(locale))
            {
                await AtomicFileWriter.WriteJsonAsync(
                    Path.Combine(tempDirectory, ProjectConstants.ProjectFileName),
                    project,
                    ProjectFileService.CreateJsonOptions(),
                    processCancellationToken).ConfigureAwait(false);
            }

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet")
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                }
            };
            var arguments = new List<string> { "run", "--no-build", "--project", runtimeProject, "--", "--play-ir", irPath };
            if (!string.IsNullOrWhiteSpace(locale))
            {
                arguments.Add("--locale");
                arguments.Add(locale);
            }

            arguments.Add("--json-trace");
            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync(processCancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(processCancellationToken);
            try
            {
                await process.WaitForExitAsync(processCancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                throw new InvalidOperationException("Runtime preview process timed out after 30 seconds.");
            }
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Runtime preview process failed: {stderr.Trim()}");
            }

            return JsonSerializer.Deserialize<RuntimeTraceResult>(
                    stdout,
                    ProjectFileService.CreateJsonOptions()) ??
                throw new InvalidDataException("Runtime preview process returned empty trace JSON.");
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
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

    private static string? ResolveRuntimeProjectPath()
    {
        foreach (var root in CandidateRoots())
        {
            var candidate = Path.Combine(root, "src", "Pompo.Runtime.Fna", "Pompo.Runtime.Fna.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateRoots()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            yield return current.FullName;
            current = current.Parent;
        }

        current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }

}

internal static class GraphPreviewLocalizerFactory
{
    public static StringTableLocalizer? Create(PompoProjectDocument? project, string? locale)
    {
        if (project is null || string.IsNullOrWhiteSpace(locale))
        {
            return null;
        }

        var fallbackLocale = project.SupportedLocales.FirstOrDefault(
            supported => !string.Equals(supported, locale, StringComparison.Ordinal));
        return new StringTableLocalizer(project.StringTables, locale, fallbackLocale);
    }
}
