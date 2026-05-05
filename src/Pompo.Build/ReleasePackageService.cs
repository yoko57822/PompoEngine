using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Pompo.Core.Project;

namespace Pompo.Build;

public sealed record ReleasePackageManifest(
    string PackageName,
    string BuildAppName,
    string BuildVersion,
    PompoTargetPlatform BuildPlatform,
    bool BuildSelfContained,
    IReadOnlyList<string> SupportedLocales,
    IReadOnlyList<string> SmokeTestedLocales,
    IReadOnlyList<string> IncludedFiles,
    IReadOnlyList<string> CompiledGraphs,
    string ArchivePath,
    string Sha256Path,
    string Sha256,
    long ArchiveSizeBytes,
    DateTimeOffset CreatedAt);

public sealed record ReleaseVerificationDiagnostic(string Code, string Message, string? Path = null);

public sealed record ReleaseVerificationOptions(
    bool RequireSmokeTestedLocales = false,
    bool RequireSelfContained = false);

public sealed record ReleaseVerificationResult(
    bool IsValid,
    ReleasePackageManifest? Manifest,
    IReadOnlyList<ReleaseVerificationDiagnostic> Diagnostics);

public sealed record ReleaseSignatureResult(
    string SignaturePath,
    string Algorithm,
    long SignatureSizeBytes);

public sealed record ReleaseSignatureVerificationResult(
    bool IsValid,
    IReadOnlyList<ReleaseVerificationDiagnostic> Diagnostics);

public sealed class ReleasePackageService
{
    private const string ProjectDataArchivePath = "Data/project.pompo.json";

    public async Task<ReleasePackageManifest> PackageAsync(
        string buildOutputDirectory,
        string releaseOutputDirectory,
        string packageName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(buildOutputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(releaseOutputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        ValidatePackageName(packageName);

        if (!Directory.Exists(buildOutputDirectory))
        {
            throw new DirectoryNotFoundException($"Build output directory '{buildOutputDirectory}' does not exist.");
        }

        var buildManifest = await LoadBuildManifestAsync(buildOutputDirectory, cancellationToken).ConfigureAwait(false);

        Directory.CreateDirectory(releaseOutputDirectory);
        var archivePath = Path.Combine(releaseOutputDirectory, $"{packageName}.zip");
        var checksumPath = Path.Combine(releaseOutputDirectory, $"{packageName}.zip.sha256");
        var manifestPath = Path.Combine(releaseOutputDirectory, $"{packageName}.release.json");

        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        ZipFile.CreateFromDirectory(buildOutputDirectory, archivePath, CompressionLevel.Optimal, includeBaseDirectory: false);
        var hash = await ComputeSha256Async(archivePath, cancellationToken).ConfigureAwait(false);
        await AtomicFileWriter.WriteTextAsync(checksumPath, $"{hash}  {Path.GetFileName(archivePath)}{Environment.NewLine}", cancellationToken)
            .ConfigureAwait(false);

        var manifest = new ReleasePackageManifest(
            packageName,
            buildManifest.AppName,
            buildManifest.Version,
            buildManifest.Platform,
            buildManifest.SelfContained,
            buildManifest.SupportedLocales,
            buildManifest.SmokeTestedLocales,
            buildManifest.IncludedFiles,
            buildManifest.CompiledGraphs,
            archivePath,
            checksumPath,
            hash,
            new FileInfo(archivePath).Length,
            DateTimeOffset.UtcNow);

        await AtomicFileWriter.WriteJsonAsync(
            manifestPath,
            manifest,
            ProjectFileService.CreateJsonOptions(),
            cancellationToken).ConfigureAwait(false);

        return manifest;
    }

    public Task<ReleaseVerificationResult> VerifyAsync(
        string manifestPath,
        CancellationToken cancellationToken)
    {
        return VerifyAsync(manifestPath, null, cancellationToken);
    }

    public async Task<ReleaseVerificationResult> VerifyAsync(
        string manifestPath,
        ReleaseVerificationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        options ??= new ReleaseVerificationOptions();

        if (!File.Exists(manifestPath))
        {
            return new ReleaseVerificationResult(
                false,
                null,
                [new ReleaseVerificationDiagnostic("REL001", $"Release manifest '{manifestPath}' does not exist.", manifestPath)]);
        }

        ReleasePackageManifest? manifest;
        try
        {
            await using var stream = File.OpenRead(manifestPath);
            manifest = await JsonSerializer.DeserializeAsync<ReleasePackageManifest>(
                stream,
                ProjectFileService.CreateJsonOptions(),
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            return new ReleaseVerificationResult(
                false,
                null,
                [new ReleaseVerificationDiagnostic("REL002", $"Release manifest is invalid JSON: {ex.Message}", manifestPath)]);
        }

        if (manifest is null)
        {
            return new ReleaseVerificationResult(
                false,
                null,
                [new ReleaseVerificationDiagnostic("REL003", "Release manifest is empty.", manifestPath)]);
        }

        var diagnostics = new List<ReleaseVerificationDiagnostic>();
        if (string.IsNullOrWhiteSpace(manifest.PackageName))
        {
            diagnostics.Add(new ReleaseVerificationDiagnostic("REL004", "Release manifest has an empty package name.", manifestPath));
        }
        else if (!IsValidPackageName(manifest.PackageName))
        {
            diagnostics.Add(new ReleaseVerificationDiagnostic(
                "REL033",
                $"Release manifest package name '{manifest.PackageName}' is not a safe release file name.",
                manifestPath));
        }
        else
        {
            var expectedArchiveName = $"{manifest.PackageName}.zip";
            var expectedChecksumName = $"{manifest.PackageName}.zip.sha256";
            var expectedManifestName = $"{manifest.PackageName}.release.json";
            if (!string.Equals(Path.GetFileName(manifest.ArchivePath), expectedArchiveName, StringComparison.Ordinal))
            {
                diagnostics.Add(new ReleaseVerificationDiagnostic(
                    "REL034",
                    $"Release manifest archive path must end with '{expectedArchiveName}'.",
                    manifest.ArchivePath));
            }

            if (!string.Equals(Path.GetFileName(manifest.Sha256Path), expectedChecksumName, StringComparison.Ordinal))
            {
                diagnostics.Add(new ReleaseVerificationDiagnostic(
                    "REL035",
                    $"Release manifest checksum path must end with '{expectedChecksumName}'.",
                    manifest.Sha256Path));
            }

            if (!string.Equals(Path.GetFileName(manifestPath), expectedManifestName, StringComparison.Ordinal))
            {
                diagnostics.Add(new ReleaseVerificationDiagnostic(
                    "REL036",
                    $"Release manifest file name must be '{expectedManifestName}'.",
                    manifestPath));
            }
        }

        if (string.IsNullOrWhiteSpace(manifest.BuildAppName))
        {
            diagnostics.Add(new ReleaseVerificationDiagnostic("REL015", "Release manifest has an empty build app name.", manifestPath));
        }

        if (string.IsNullOrWhiteSpace(manifest.BuildVersion))
        {
            diagnostics.Add(new ReleaseVerificationDiagnostic("REL016", "Release manifest has an empty build version.", manifestPath));
        }

        if (options.RequireSelfContained && !manifest.BuildSelfContained)
        {
            diagnostics.Add(new ReleaseVerificationDiagnostic(
                "REL027",
                "Release manifest was built as framework-dependent, but self-contained runtime output is required.",
                manifestPath));
        }

        var supportedLocales = manifest.SupportedLocales ?? [];
        var smokeTestedLocales = manifest.SmokeTestedLocales ?? [];
        var includedFiles = manifest.IncludedFiles ?? [];
        var compiledGraphs = manifest.CompiledGraphs ?? [];
        if (supportedLocales.Count == 0)
        {
            diagnostics.Add(new ReleaseVerificationDiagnostic("REL017", "Release manifest does not list supported locales.", manifestPath));
        }

        var supportedLookup = supportedLocales.ToHashSet(StringComparer.Ordinal);
        foreach (var locale in smokeTestedLocales)
        {
            if (!supportedLookup.Contains(locale))
            {
                diagnostics.Add(new ReleaseVerificationDiagnostic(
                    "REL018",
                    $"Release manifest lists smoke-tested locale '{locale}' that is not supported by the build.",
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
                diagnostics.Add(new ReleaseVerificationDiagnostic(
                    "REL026",
                    $"Release manifest has supported locales without packaged-runtime smoke coverage: {string.Join(", ", missingSmokeLocales)}.",
                    manifestPath));
            }
        }

        if (includedFiles.Count == 0)
        {
            diagnostics.Add(new ReleaseVerificationDiagnostic("REL019", "Release manifest does not list included build files.", manifestPath));
        }

        var includedLookup = includedFiles
            .Select(NormalizeArchivePath)
            .ToHashSet(StringComparer.Ordinal);
        if (!includedLookup.Contains(ProjectDataArchivePath))
        {
            diagnostics.Add(new ReleaseVerificationDiagnostic(
                "REL030",
                $"Release manifest does not list required project data '{ProjectDataArchivePath}'.",
                manifestPath));
        }

        foreach (var includedFile in includedFiles)
        {
            var normalized = NormalizeArchivePath(includedFile);
            if (IsForbiddenRuntimeArtifact(normalized))
            {
                diagnostics.Add(new ReleaseVerificationDiagnostic(
                    "REL028",
                    $"Release manifest lists forbidden runtime artifact '{normalized}'.",
                    manifestPath));
            }

            if (IsSourceScriptArtifact(normalized))
            {
                diagnostics.Add(new ReleaseVerificationDiagnostic(
                    "REL037",
                    $"Release manifest lists source script artifact '{normalized}'.",
                    manifestPath));
            }

            if (IsDebugSymbolArtifact(normalized))
            {
                diagnostics.Add(new ReleaseVerificationDiagnostic(
                    "REL038",
                    $"Release manifest lists debug symbol artifact '{normalized}'.",
                    manifestPath));
            }
        }

        if (compiledGraphs.Count == 0)
        {
            diagnostics.Add(new ReleaseVerificationDiagnostic("REL031", "Release manifest does not list any compiled graphs.", manifestPath));
        }

        foreach (var graph in compiledGraphs)
        {
            var expectedPath = NormalizeArchivePath(Path.Combine("Data", graph));
            if (!includedLookup.Contains(expectedPath))
            {
                diagnostics.Add(new ReleaseVerificationDiagnostic(
                    "REL022",
                    $"Compiled graph '{graph}' is not listed as included file '{expectedPath}'.",
                    manifestPath));
            }
        }

        if (!File.Exists(manifest.ArchivePath))
        {
            diagnostics.Add(new ReleaseVerificationDiagnostic("REL005", $"Release archive '{manifest.ArchivePath}' does not exist.", manifest.ArchivePath));
        }

        if (!File.Exists(manifest.Sha256Path))
        {
            diagnostics.Add(new ReleaseVerificationDiagnostic("REL006", $"Release checksum file '{manifest.Sha256Path}' does not exist.", manifest.Sha256Path));
        }

        if (File.Exists(manifest.ArchivePath))
        {
            var archiveInfo = new FileInfo(manifest.ArchivePath);
            if (archiveInfo.Length != manifest.ArchiveSizeBytes)
            {
                diagnostics.Add(new ReleaseVerificationDiagnostic(
                    "REL007",
                    $"Release archive size is {archiveInfo.Length} bytes, expected {manifest.ArchiveSizeBytes}.",
                    manifest.ArchivePath));
            }

            var actualHash = await ComputeSha256Async(manifest.ArchivePath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(actualHash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new ReleaseVerificationDiagnostic(
                    "REL008",
                    $"Release archive SHA-256 is '{actualHash}', expected '{manifest.Sha256}'.",
                    manifest.ArchivePath));
            }

            diagnostics.AddRange(await VerifyArchiveContentsAsync(
                manifest.ArchivePath,
                manifest,
                options,
                cancellationToken).ConfigureAwait(false));
        }

        if (File.Exists(manifest.Sha256Path))
        {
            var checksumText = await File.ReadAllTextAsync(manifest.Sha256Path, cancellationToken).ConfigureAwait(false);
            var checksumHash = checksumText
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (!string.Equals(checksumHash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new ReleaseVerificationDiagnostic(
                    "REL009",
                    $"Release checksum file records '{checksumHash ?? "<empty>"}', expected '{manifest.Sha256}'.",
                    manifest.Sha256Path));
            }

            if (!checksumText.Contains(Path.GetFileName(manifest.ArchivePath), StringComparison.Ordinal))
            {
                diagnostics.Add(new ReleaseVerificationDiagnostic(
                    "REL010",
                    $"Release checksum file does not name archive '{Path.GetFileName(manifest.ArchivePath)}'.",
                    manifest.Sha256Path));
            }
        }

        return new ReleaseVerificationResult(diagnostics.Count == 0, manifest, diagnostics);
    }

    private static async Task<IReadOnlyList<ReleaseVerificationDiagnostic>> VerifyArchiveContentsAsync(
        string archivePath,
        ReleasePackageManifest manifest,
        ReleaseVerificationOptions options,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<ReleaseVerificationDiagnostic>();
        ZipArchive archive;
        try
        {
            archive = ZipFile.OpenRead(archivePath);
        }
        catch (InvalidDataException ex)
        {
            return
            [
                new ReleaseVerificationDiagnostic(
                    "REL023",
                    $"Release archive is not a valid zip file: {ex.Message}",
                    archivePath)
            ];
        }

        using (archive)
        {
            var entries = archive.Entries
                .Select(entry => NormalizeArchivePath(entry.FullName))
                .ToHashSet(StringComparer.Ordinal);

            var buildManifestEntry = archive.GetEntry("pompo-build-manifest.json");
            if (buildManifestEntry is null)
            {
                diagnostics.Add(new ReleaseVerificationDiagnostic(
                    "REL020",
                    "Release archive does not contain pompo-build-manifest.json.",
                    archivePath));
            }
            else
            {
                var embeddedManifest = await ReadEmbeddedBuildManifestAsync(
                    buildManifestEntry,
                    archivePath,
                    diagnostics,
                    cancellationToken).ConfigureAwait(false);
                if (embeddedManifest is not null)
                {
                    diagnostics.AddRange(CompareEmbeddedBuildManifest(archivePath, manifest, embeddedManifest));
                }
            }

            foreach (var includedFile in manifest.IncludedFiles ?? [])
            {
                var archiveEntry = NormalizeArchivePath(includedFile);
                if (!entries.Contains(archiveEntry))
                {
                    diagnostics.Add(new ReleaseVerificationDiagnostic(
                        "REL021",
                        $"Release archive does not contain included build file '{archiveEntry}'.",
                        archivePath));
                }
            }

            foreach (var entry in entries)
            {
                if (IsForbiddenRuntimeArtifact(entry))
                {
                    diagnostics.Add(new ReleaseVerificationDiagnostic(
                        "REL028",
                        $"Release archive contains forbidden runtime artifact '{entry}'.",
                        archivePath));
                }

                if (IsSourceScriptArtifact(entry))
                {
                    diagnostics.Add(new ReleaseVerificationDiagnostic(
                        "REL037",
                        $"Release archive contains source script artifact '{entry}'.",
                        archivePath));
                }

                if (IsDebugSymbolArtifact(entry))
                {
                    diagnostics.Add(new ReleaseVerificationDiagnostic(
                        "REL038",
                        $"Release archive contains debug symbol artifact '{entry}'.",
                        archivePath));
                }
            }

            var allowedEntries = (manifest.IncludedFiles ?? [])
                .Select(NormalizeArchivePath)
                .Append("pompo-build-manifest.json")
                .ToHashSet(StringComparer.Ordinal);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Name))
                {
                    continue;
                }

                var archiveEntry = NormalizeArchivePath(entry.FullName);
                if (!allowedEntries.Contains(archiveEntry))
                {
                    diagnostics.Add(new ReleaseVerificationDiagnostic(
                        "REL032",
                        $"Release archive contains file not listed in build manifest '{archiveEntry}'.",
                        archivePath));
                }
            }

            if (options.RequireSelfContained &&
                !entries.Contains(GetRuntimeExecutableArchivePath(manifest.BuildPlatform)))
            {
                diagnostics.Add(new ReleaseVerificationDiagnostic(
                    "REL029",
                    $"Release archive does not contain runtime executable '{GetRuntimeExecutableArchivePath(manifest.BuildPlatform)}'.",
                    archivePath));
            }

            return diagnostics;
        }
    }

    private static async Task<BuildArtifactManifest?> ReadEmbeddedBuildManifestAsync(
        ZipArchiveEntry entry,
        string archivePath,
        List<ReleaseVerificationDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = entry.Open();
            return await JsonSerializer.DeserializeAsync<BuildArtifactManifest>(
                stream,
                ProjectFileService.CreateJsonOptions(),
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            diagnostics.Add(new ReleaseVerificationDiagnostic(
                "REL024",
                $"Embedded build manifest is invalid JSON: {ex.Message}",
                archivePath));
            return null;
        }
    }

    private static IEnumerable<ReleaseVerificationDiagnostic> CompareEmbeddedBuildManifest(
        string archivePath,
        ReleasePackageManifest releaseManifest,
        BuildArtifactManifest buildManifest)
    {
        if (!string.Equals(releaseManifest.BuildAppName, buildManifest.AppName, StringComparison.Ordinal) ||
            !string.Equals(releaseManifest.BuildVersion, buildManifest.Version, StringComparison.Ordinal) ||
            releaseManifest.BuildPlatform != buildManifest.Platform ||
            releaseManifest.BuildSelfContained != buildManifest.SelfContained ||
            !SequenceEqual(releaseManifest.SupportedLocales, buildManifest.SupportedLocales) ||
            !SequenceEqual(releaseManifest.SmokeTestedLocales, buildManifest.SmokeTestedLocales) ||
            !SequenceEqual(releaseManifest.IncludedFiles, buildManifest.IncludedFiles) ||
            !SequenceEqual(releaseManifest.CompiledGraphs, buildManifest.CompiledGraphs))
        {
            yield return new ReleaseVerificationDiagnostic(
                "REL025",
                "Release manifest metadata does not match the embedded build manifest.",
                archivePath);
        }
    }

    private static bool SequenceEqual<T>(IReadOnlyList<T>? left, IReadOnlyList<T>? right)
    {
        return (left ?? []).SequenceEqual(right ?? []);
    }

    private static async Task<BuildArtifactManifest> LoadBuildManifestAsync(
        string buildOutputDirectory,
        CancellationToken cancellationToken)
    {
        var buildManifestPath = Path.Combine(buildOutputDirectory, "pompo-build-manifest.json");
        if (!File.Exists(buildManifestPath))
        {
            throw new InvalidDataException($"Build manifest '{buildManifestPath}' does not exist.");
        }

        BuildArtifactManifest? buildManifest;
        try
        {
            await using var stream = File.OpenRead(buildManifestPath);
            buildManifest = await JsonSerializer.DeserializeAsync<BuildArtifactManifest>(
                stream,
                ProjectFileService.CreateJsonOptions(),
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Build manifest '{buildManifestPath}' is invalid JSON: {ex.Message}", ex);
        }

        if (buildManifest is null)
        {
            throw new InvalidDataException($"Build manifest '{buildManifestPath}' is empty.");
        }

        var diagnostics = ValidateBuildManifestForPackage(buildManifest).ToList();
        diagnostics.AddRange(ValidateBuildManifestFilesExist(buildOutputDirectory, buildManifest));
        diagnostics.AddRange(ValidateBuildOutputFilesMatchManifest(buildOutputDirectory, buildManifest));
        diagnostics.AddRange(ValidateBuildOutputDoesNotContainEditorArtifacts(buildOutputDirectory));
        if (diagnostics.Count > 0)
        {
            throw new InvalidDataException(
                $"Build manifest '{buildManifestPath}' is not packageable: {string.Join(" ", diagnostics)}");
        }

        return buildManifest;
    }

    private static IEnumerable<string> ValidateBuildManifestFilesExist(
        string buildOutputDirectory,
        BuildArtifactManifest manifest)
    {
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
                yield return $"Included file '{normalized}' does not exist in build output.";
            }
        }
    }

    private static IEnumerable<string> ValidateBuildOutputFilesMatchManifest(
        string buildOutputDirectory,
        BuildArtifactManifest manifest)
    {
        var allowedFiles = (manifest.IncludedFiles ?? [])
            .Select(NormalizeArchivePath)
            .Append("pompo-build-manifest.json")
            .ToHashSet(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(buildOutputDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = NormalizeArchivePath(Path.GetRelativePath(buildOutputDirectory, file));
            if (!allowedFiles.Contains(relativePath))
            {
                yield return $"Build output contains unmanifested file '{relativePath}'.";
            }
        }
    }

    private static IEnumerable<string> ValidateBuildOutputDoesNotContainEditorArtifacts(string buildOutputDirectory)
    {
        foreach (var file in Directory.EnumerateFiles(buildOutputDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = NormalizeArchivePath(Path.GetRelativePath(buildOutputDirectory, file));
            if (IsForbiddenRuntimeArtifact(relativePath))
            {
                yield return $"Build output contains forbidden runtime artifact '{relativePath}'.";
            }

            if (IsSourceScriptArtifact(relativePath))
            {
                yield return $"Build output contains source script artifact '{relativePath}'.";
            }

            if (IsDebugSymbolArtifact(relativePath))
            {
                yield return $"Build output contains debug symbol artifact '{relativePath}'.";
            }
        }
    }

    private static IEnumerable<string> ValidateBuildManifestForPackage(BuildArtifactManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.AppName))
        {
            yield return "AppName is empty.";
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            yield return "Version is empty.";
        }

        if (manifest.IncludedFiles is null || manifest.IncludedFiles.Count == 0)
        {
            yield return "IncludedFiles is empty.";
        }
        else
        {
            var includedLookup = new HashSet<string>(StringComparer.Ordinal);
            foreach (var includedFile in manifest.IncludedFiles)
            {
                var normalized = NormalizeArchivePath(includedFile);
                if (!IsSafeRelativeArchivePath(normalized))
                {
                    yield return $"IncludedFiles contains invalid relative path '{includedFile}'.";
                    continue;
                }

                if (IsForbiddenRuntimeArtifact(normalized))
                {
                    yield return $"IncludedFiles contains forbidden runtime artifact '{normalized}'.";
                }

                if (IsSourceScriptArtifact(normalized))
                {
                    yield return $"IncludedFiles contains source script artifact '{normalized}'.";
                }

                if (IsDebugSymbolArtifact(normalized))
                {
                    yield return $"IncludedFiles contains debug symbol artifact '{normalized}'.";
                }

                if (!includedLookup.Add(normalized))
                {
                    yield return $"IncludedFiles contains duplicate path '{normalized}'.";
                }
            }

            if (!includedLookup.Contains(ProjectDataArchivePath))
            {
                yield return $"IncludedFiles does not contain required project data '{ProjectDataArchivePath}'.";
            }
        }

        if (manifest.SupportedLocales is null || manifest.SupportedLocales.Count == 0)
        {
            yield return "SupportedLocales is empty.";
            yield break;
        }

        var supportedLookup = manifest.SupportedLocales.ToHashSet(StringComparer.Ordinal);
        if (supportedLookup.Count != manifest.SupportedLocales.Count)
        {
            yield return "SupportedLocales contains duplicates.";
        }

        if (manifest.SmokeTestedLocales is null)
        {
            yield return "SmokeTestedLocales is missing.";
            yield break;
        }

        foreach (var locale in manifest.SmokeTestedLocales)
        {
            if (!supportedLookup.Contains(locale))
            {
                yield return $"SmokeTestedLocales contains unsupported locale '{locale}'.";
            }
        }

        if (manifest.CompiledGraphs is null)
        {
            yield return "CompiledGraphs is missing.";
            yield break;
        }

        if (manifest.CompiledGraphs.Count == 0)
        {
            yield return "CompiledGraphs is empty.";
            yield break;
        }

        var includedFiles = manifest.IncludedFiles?
            .Select(NormalizeArchivePath)
            .ToHashSet(StringComparer.Ordinal) ?? [];
        foreach (var graph in manifest.CompiledGraphs)
        {
            if (!IsSafeRelativeArchivePath(graph) || graph.Contains('/'))
            {
                yield return $"CompiledGraphs contains invalid graph file '{graph}'.";
                continue;
            }

            var expectedPath = NormalizeArchivePath(Path.Combine("Data", graph));
            if (!includedFiles.Contains(expectedPath))
            {
                yield return $"CompiledGraphs contains '{graph}' but IncludedFiles does not contain '{expectedPath}'.";
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

    private static void ValidatePackageName(string packageName)
    {
        if (!IsValidPackageName(packageName))
        {
            throw new ArgumentException("Release package name can use letters, numbers, '-', '_', or '.', and must not start or end with '.'.", nameof(packageName));
        }
    }

    private static bool IsValidPackageName(string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName) ||
            !string.Equals(packageName, packageName.Trim(), StringComparison.Ordinal) ||
            packageName is "." or ".." ||
            packageName.StartsWith(".", StringComparison.Ordinal) ||
            packageName.EndsWith(".", StringComparison.Ordinal))
        {
            return false;
        }

        return packageName.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');
    }

    private static bool IsForbiddenRuntimeArtifact(string path)
    {
        var fileName = Path.GetFileName(NormalizeArchivePath(path));
        return fileName.StartsWith("Pompo.Editor", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("Avalonia", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("Pompo.Build", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("Pompo.Cli", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("Pompo.Tests", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("testhost", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSourceScriptArtifact(string path)
    {
        var normalized = NormalizeArchivePath(path);
        return normalized.StartsWith("Scripts/", StringComparison.Ordinal) &&
            normalized.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDebugSymbolArtifact(string path)
    {
        return NormalizeArchivePath(path).EndsWith(".pdb", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetRuntimeExecutableArchivePath(PompoTargetPlatform platform)
    {
        var executableName = platform == PompoTargetPlatform.Windows
            ? "Pompo.Runtime.Fna.exe"
            : "Pompo.Runtime.Fna";
        return $"Runtime/{executableName}";
    }

    public async Task<ReleaseSignatureResult> SignAsync(
        string manifestPath,
        string privateKeyPemPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPemPath);

        var verified = await VerifyAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        if (!verified.IsValid || verified.Manifest is null)
        {
            throw new InvalidDataException("Release must pass verification before it can be signed.");
        }

        if (!File.Exists(privateKeyPemPath))
        {
            throw new FileNotFoundException("Private key PEM file does not exist.", privateKeyPemPath);
        }

        using var rsa = RSA.Create();
        rsa.ImportFromPem(await File.ReadAllTextAsync(privateKeyPemPath, cancellationToken).ConfigureAwait(false));
        await using var archiveStream = File.OpenRead(verified.Manifest.ArchivePath);
        var signature = rsa.SignData(archiveStream, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signaturePath = $"{verified.Manifest.ArchivePath}.sig";
        await AtomicFileWriter.WriteTextAsync(
            signaturePath,
            Convert.ToBase64String(signature) + Environment.NewLine,
            cancellationToken).ConfigureAwait(false);

        return new ReleaseSignatureResult(signaturePath, "RSA-SHA256-PKCS1", signature.Length);
    }

    public async Task<ReleaseSignatureVerificationResult> VerifySignatureAsync(
        string manifestPath,
        string publicKeyPemPath,
        string? signaturePath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyPemPath);

        var verified = await VerifyAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        var diagnostics = verified.Diagnostics.ToList();
        if (!verified.IsValid || verified.Manifest is null)
        {
            return new ReleaseSignatureVerificationResult(false, diagnostics);
        }

        if (!File.Exists(publicKeyPemPath))
        {
            diagnostics.Add(new ReleaseVerificationDiagnostic(
                "REL011",
                $"Public key PEM file '{publicKeyPemPath}' does not exist.",
                publicKeyPemPath));
            return new ReleaseSignatureVerificationResult(false, diagnostics);
        }

        var resolvedSignaturePath = signaturePath ?? $"{verified.Manifest.ArchivePath}.sig";
        if (!File.Exists(resolvedSignaturePath))
        {
            diagnostics.Add(new ReleaseVerificationDiagnostic(
                "REL012",
                $"Release signature file '{resolvedSignaturePath}' does not exist.",
                resolvedSignaturePath));
            return new ReleaseSignatureVerificationResult(false, diagnostics);
        }

        byte[] signature;
        try
        {
            var signatureText = await File.ReadAllTextAsync(resolvedSignaturePath, cancellationToken).ConfigureAwait(false);
            signature = Convert.FromBase64String(signatureText.Trim());
        }
        catch (FormatException ex)
        {
            diagnostics.Add(new ReleaseVerificationDiagnostic(
                "REL013",
                $"Release signature file is not valid Base64: {ex.Message}",
                resolvedSignaturePath));
            return new ReleaseSignatureVerificationResult(false, diagnostics);
        }

        using var rsa = RSA.Create();
        rsa.ImportFromPem(await File.ReadAllTextAsync(publicKeyPemPath, cancellationToken).ConfigureAwait(false));
        await using var archiveStream = File.OpenRead(verified.Manifest.ArchivePath);
        if (!rsa.VerifyData(archiveStream, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
        {
            diagnostics.Add(new ReleaseVerificationDiagnostic(
                "REL014",
                "Release signature does not match the archive and public key.",
                resolvedSignaturePath));
        }

        return new ReleaseSignatureVerificationResult(diagnostics.Count == 0, diagnostics);
    }

    public static async Task<string> ComputeSha256Async(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
