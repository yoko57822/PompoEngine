using System.Text.Json;
using Pompo.Core.Project;

namespace Pompo.Build;

public sealed record BuildOutputVerificationOptions(
    bool RequireSmokeTestedLocales = false,
    bool RequireSelfContained = false);

public sealed record BuildOutputVerificationDiagnostic(string Code, string Message, string? Path = null);

public sealed record BuildOutputVerificationResult(
    bool IsValid,
    BuildArtifactManifest? Manifest,
    IReadOnlyList<BuildOutputVerificationDiagnostic> Diagnostics);

public sealed class BuildOutputVerificationService
{
    private const string BuildManifestFileName = "pompo-build-manifest.json";
    private const string ProjectDataPath = "Data/project.pompo.json";

    public async Task<BuildOutputVerificationResult> VerifyAsync(
        string buildOutputDirectory,
        BuildOutputVerificationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(buildOutputDirectory);
        options ??= new BuildOutputVerificationOptions();
        var diagnostics = new List<BuildOutputVerificationDiagnostic>();

        if (!Directory.Exists(buildOutputDirectory))
        {
            return new BuildOutputVerificationResult(
                false,
                null,
                [new BuildOutputVerificationDiagnostic("BVERIFY001", $"Build output directory '{buildOutputDirectory}' does not exist.", buildOutputDirectory)]);
        }

        var manifestPath = Path.Combine(buildOutputDirectory, BuildManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return new BuildOutputVerificationResult(
                false,
                null,
                [new BuildOutputVerificationDiagnostic("BVERIFY002", $"Build manifest '{manifestPath}' does not exist.", manifestPath)]);
        }

        BuildArtifactManifest? manifest;
        try
        {
            await using var stream = File.OpenRead(manifestPath);
            manifest = await JsonSerializer.DeserializeAsync<BuildArtifactManifest>(
                stream,
                ProjectFileService.CreateJsonOptions(),
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            return new BuildOutputVerificationResult(
                false,
                null,
                [new BuildOutputVerificationDiagnostic("BVERIFY003", $"Build manifest is invalid JSON: {ex.Message}", manifestPath)]);
        }

        if (manifest is null)
        {
            return new BuildOutputVerificationResult(
                false,
                null,
                [new BuildOutputVerificationDiagnostic("BVERIFY004", "Build manifest is empty.", manifestPath)]);
        }

        ValidateManifest(manifest, manifestPath, options, diagnostics);
        ValidateBuildOutput(buildOutputDirectory, manifest, diagnostics);
        return new BuildOutputVerificationResult(diagnostics.Count == 0, manifest, diagnostics);
    }

    private static void ValidateManifest(
        BuildArtifactManifest manifest,
        string manifestPath,
        BuildOutputVerificationOptions options,
        ICollection<BuildOutputVerificationDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(manifest.AppName))
        {
            diagnostics.Add(new BuildOutputVerificationDiagnostic("BVERIFY005", "Build manifest has an empty appName.", manifestPath));
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            diagnostics.Add(new BuildOutputVerificationDiagnostic("BVERIFY006", "Build manifest has an empty version.", manifestPath));
        }

        if (options.RequireSelfContained && !manifest.SelfContained)
        {
            diagnostics.Add(new BuildOutputVerificationDiagnostic(
                "BVERIFY024",
                "Build manifest was produced as framework-dependent, but self-contained runtime output is required.",
                manifestPath));
        }

        var includedFiles = manifest.IncludedFiles ?? [];
        if (includedFiles.Count == 0)
        {
            diagnostics.Add(new BuildOutputVerificationDiagnostic("BVERIFY007", "Build manifest has no included files.", manifestPath));
        }

        var includedLookup = new HashSet<string>(StringComparer.Ordinal);
        foreach (var includedFile in includedFiles)
        {
            var normalized = NormalizeArchivePath(includedFile);
            if (!IsSafeRelativeArchivePath(normalized))
            {
                diagnostics.Add(new BuildOutputVerificationDiagnostic(
                    "BVERIFY008",
                    $"Build manifest contains invalid included file path '{includedFile}'.",
                    manifestPath));
                continue;
            }

            if (IsForbiddenRuntimeArtifact(normalized))
            {
                diagnostics.Add(new BuildOutputVerificationDiagnostic(
                    "BVERIFY009",
                    $"Build manifest lists forbidden runtime artifact '{normalized}'.",
                    manifestPath));
            }

            if (IsSourceScriptArtifact(normalized))
            {
                diagnostics.Add(new BuildOutputVerificationDiagnostic(
                    "BVERIFY026",
                    $"Build manifest lists source script artifact '{normalized}'.",
                    manifestPath));
            }

            if (IsDebugSymbolArtifact(normalized))
            {
                diagnostics.Add(new BuildOutputVerificationDiagnostic(
                    "BVERIFY028",
                    $"Build manifest lists debug symbol artifact '{normalized}'.",
                    manifestPath));
            }

            if (!includedLookup.Add(normalized))
            {
                diagnostics.Add(new BuildOutputVerificationDiagnostic(
                    "BVERIFY010",
                    $"Build manifest contains duplicate included file '{normalized}'.",
                    manifestPath));
            }
        }

        if (!includedLookup.Contains(ProjectDataPath))
        {
            diagnostics.Add(new BuildOutputVerificationDiagnostic(
                "BVERIFY011",
                $"Build manifest does not list required project data '{ProjectDataPath}'.",
                manifestPath));
        }

        var supportedLocales = manifest.SupportedLocales ?? [];
        if (supportedLocales.Count == 0)
        {
            diagnostics.Add(new BuildOutputVerificationDiagnostic("BVERIFY012", "Build manifest has no supported locales.", manifestPath));
        }

        var supportedLookup = supportedLocales.ToHashSet(StringComparer.Ordinal);
        if (supportedLookup.Count != supportedLocales.Count)
        {
            diagnostics.Add(new BuildOutputVerificationDiagnostic("BVERIFY013", "Build manifest has duplicate supported locales.", manifestPath));
        }

        var smokeTestedLocales = manifest.SmokeTestedLocales ?? [];
        foreach (var locale in smokeTestedLocales)
        {
            if (!supportedLookup.Contains(locale))
            {
                diagnostics.Add(new BuildOutputVerificationDiagnostic(
                    "BVERIFY015",
                    $"Build manifest lists smoke-tested locale '{locale}' that is not supported.",
                    manifestPath));
            }
        }

        if (options.RequireSmokeTestedLocales)
        {
            var smokeLookup = smokeTestedLocales.ToHashSet(StringComparer.Ordinal);
            var missingSmokeLocales = supportedLocales
                .Where(locale => !smokeLookup.Contains(locale))
                .ToArray();
            if (missingSmokeLocales.Length > 0)
            {
                diagnostics.Add(new BuildOutputVerificationDiagnostic(
                    "BVERIFY023",
                    $"Build manifest has supported locales without packaged-runtime smoke coverage: {string.Join(", ", missingSmokeLocales)}.",
                    manifestPath));
            }
        }

        var compiledGraphs = manifest.CompiledGraphs ?? [];
        if (compiledGraphs.Count == 0)
        {
            diagnostics.Add(new BuildOutputVerificationDiagnostic("BVERIFY017", "Build manifest has no compiled graphs.", manifestPath));
        }

        foreach (var graph in compiledGraphs)
        {
            if (!IsSafeRelativeArchivePath(graph) || graph.Contains('/'))
            {
                diagnostics.Add(new BuildOutputVerificationDiagnostic(
                    "BVERIFY018",
                    $"Build manifest contains invalid compiled graph file '{graph}'.",
                    manifestPath));
                continue;
            }

            var expectedPath = NormalizeArchivePath(Path.Combine("Data", graph));
            if (!includedLookup.Contains(expectedPath))
            {
                diagnostics.Add(new BuildOutputVerificationDiagnostic(
                    "BVERIFY019",
                    $"Compiled graph '{graph}' is not listed as included file '{expectedPath}'.",
                    manifestPath));
            }
        }
    }

    private static void ValidateBuildOutput(
        string buildOutputDirectory,
        BuildArtifactManifest manifest,
        ICollection<BuildOutputVerificationDiagnostic> diagnostics)
    {
        var allowedFiles = (manifest.IncludedFiles ?? [])
            .Select(NormalizeArchivePath)
            .Append(BuildManifestFileName)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var includedFile in manifest.IncludedFiles ?? [])
        {
            var normalized = NormalizeArchivePath(includedFile);
            if (!IsSafeRelativeArchivePath(normalized))
            {
                continue;
            }

            var fullPath = Path.Combine(buildOutputDirectory, normalized);
            if (!File.Exists(fullPath))
            {
                diagnostics.Add(new BuildOutputVerificationDiagnostic(
                    "BVERIFY020",
                    $"Included file '{normalized}' does not exist in build output.",
                    fullPath));
            }
        }

        foreach (var file in Directory.EnumerateFiles(buildOutputDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = NormalizeArchivePath(Path.GetRelativePath(buildOutputDirectory, file));
            if (!allowedFiles.Contains(relativePath))
            {
                diagnostics.Add(new BuildOutputVerificationDiagnostic(
                    "BVERIFY021",
                    $"Build output contains unmanifested file '{relativePath}'.",
                    file));
            }

            if (IsForbiddenRuntimeArtifact(relativePath))
            {
                diagnostics.Add(new BuildOutputVerificationDiagnostic(
                    "BVERIFY022",
                    $"Build output contains forbidden runtime artifact '{relativePath}'.",
                    file));
            }

            if (IsSourceScriptArtifact(relativePath))
            {
                diagnostics.Add(new BuildOutputVerificationDiagnostic(
                    "BVERIFY027",
                    $"Build output contains source script artifact '{relativePath}'.",
                    file));
            }

            if (IsDebugSymbolArtifact(relativePath))
            {
                diagnostics.Add(new BuildOutputVerificationDiagnostic(
                    "BVERIFY029",
                    $"Build output contains debug symbol artifact '{relativePath}'.",
                    file));
            }
        }

        if (manifest.SelfContained)
        {
            var executable = Path.Combine(buildOutputDirectory, GetRuntimeExecutablePath(manifest.Platform));
            if (!File.Exists(executable))
            {
                diagnostics.Add(new BuildOutputVerificationDiagnostic(
                    "BVERIFY025",
                    $"Self-contained build output does not contain runtime executable '{GetRuntimeExecutablePath(manifest.Platform)}'.",
                    executable));
            }
        }
    }

    private static bool IsSafeRelativeArchivePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            Path.IsPathRooted(path) ||
            path.StartsWith("/", StringComparison.Ordinal) ||
            path.Contains("../", StringComparison.Ordinal) ||
            path.Contains("/..", StringComparison.Ordinal))
        {
            return false;
        }

        return !path.Equals("..", StringComparison.Ordinal) &&
            !path.Contains('\\', StringComparison.Ordinal);
    }

    private static string NormalizeArchivePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static bool IsForbiddenRuntimeArtifact(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath);
        return fileName.StartsWith("Pompo.Editor.", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("Avalonia", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("Pompo.Build", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("Pompo.Cli", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("Pompo.Tests", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("testhost", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSourceScriptArtifact(string relativePath)
    {
        var normalized = NormalizeArchivePath(relativePath);
        return normalized.StartsWith("Scripts/", StringComparison.Ordinal) &&
            normalized.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDebugSymbolArtifact(string relativePath)
    {
        return NormalizeArchivePath(relativePath).EndsWith(".pdb", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetRuntimeExecutablePath(PompoTargetPlatform platform)
    {
        return platform switch
        {
            PompoTargetPlatform.Windows => "Runtime/Pompo.Runtime.Fna.exe",
            PompoTargetPlatform.MacOS or PompoTargetPlatform.Linux => "Runtime/Pompo.Runtime.Fna",
            _ => "Runtime/Pompo.Runtime.Fna"
        };
    }
}
