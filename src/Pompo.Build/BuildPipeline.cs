using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Pompo.Core;
using Pompo.Core.Assets;
using Pompo.Core.Project;
using Pompo.Scripting;
using Pompo.VisualScripting;

namespace Pompo.Build;

public enum PompoTargetPlatform
{
    Windows,
    MacOS,
    Linux
}

public sealed record PompoBuildProfile(
    string ProfileName,
    PompoTargetPlatform Platform,
    string AppName,
    string Version,
    string? IconPath = null,
    bool RunSmokeTest = false,
    bool PackageRuntime = true,
    string? RuntimeProjectPath = null,
    bool SelfContained = false);

public sealed record BuildArtifactManifest(
    string AppName,
    string Version,
    PompoTargetPlatform Platform,
    bool SelfContained,
    IReadOnlyList<string> IncludedFiles,
    IReadOnlyList<string> CompiledGraphs,
    IReadOnlyList<string> SupportedLocales,
    IReadOnlyList<string> SmokeTestedLocales);

public sealed record BuildDiagnostic(string Code, string Message, string? Path = null);

public sealed record BuildResult(
    bool Success,
    string OutputDirectory,
    IReadOnlyList<BuildDiagnostic> Diagnostics,
    BuildArtifactManifest? Manifest);

public sealed class PompoBuildPipeline
{
    private const string UserScriptsDirectoryName = "Scripts";
    private const string UserScriptsAssemblyName = "Pompo.UserScripts.dll";
    private readonly ProjectFileService _projectFiles = new();
    private readonly ProjectValidator _projectValidator = new();
    private readonly GraphCompiler _graphCompiler = new();

    public async Task<BuildResult> BuildAsync(
        string projectRoot,
        PompoBuildProfile profile,
        string outputRoot,
        CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<BuildDiagnostic>();
        if (!BuildProfileFileService.IsValidProfileName(profile.ProfileName))
        {
            return new BuildResult(
                false,
                Path.Combine(outputRoot, profile.Platform.ToString()),
                [new BuildDiagnostic("BUILD006", $"Build profile name '{profile.ProfileName}' is not safe for an output directory.")],
                null);
        }

        var outputDirectory = Path.Combine(outputRoot, profile.Platform.ToString(), profile.ProfileName);

        PompoProjectDocument project;
        try
        {
            project = await _projectFiles.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException)
        {
            return new BuildResult(false, outputDirectory, [new BuildDiagnostic("BUILD001", ex.Message)], null);
        }

        var projectValidation = _projectValidator.Validate(project);
        diagnostics.AddRange(projectValidation.Diagnostics.Select(diagnostic =>
            new BuildDiagnostic(diagnostic.Code, diagnostic.Message, diagnostic.DocumentPath)));

        var supportedLocales = NormalizeLocales(project.SupportedLocales);
        var compiledGraphs = new List<string>();
        foreach (var graph in project.Graphs)
        {
            try
            {
                var ir = _graphCompiler.Compile(graph);
                compiledGraphs.Add($"{ir.GraphId}.pompo-ir.json");
            }
            catch (GraphCompilationException ex)
            {
                diagnostics.AddRange(ex.Diagnostics.Select(diagnostic =>
                    new BuildDiagnostic(diagnostic.Code, diagnostic.Message, graph.GraphId)));
            }
        }

        foreach (var asset in project.Assets.Assets)
        {
            if (!AssetDatabaseService.IsSafeProjectRelativePath(asset.SourcePath))
            {
                diagnostics.Add(new BuildDiagnostic(
                    "BUILD007",
                    $"Asset source path '{asset.SourcePath}' for asset '{asset.AssetId}' is not a safe project-relative path.",
                    asset.SourcePath));
                continue;
            }

            var source = Path.Combine(projectRoot, asset.SourcePath);
            if (!File.Exists(source))
            {
                diagnostics.Add(new BuildDiagnostic(
                    "BUILD002",
                    $"Asset source file '{asset.SourcePath}' for asset '{asset.AssetId}' does not exist.",
                    asset.SourcePath));
            }
        }

        var scriptCompilation = await CompileUserScriptsAsync(projectRoot, project.ScriptPermissions, cancellationToken)
            .ConfigureAwait(false);
        diagnostics.AddRange(scriptCompilation.Diagnostics);

        if (diagnostics.Count > 0)
        {
            return new BuildResult(false, outputDirectory, diagnostics, null);
        }

        Directory.CreateDirectory(outputDirectory);
        var dataDirectory = Path.Combine(outputDirectory, "Data");
        var assetDirectory = Path.Combine(outputDirectory, "Assets");
        var runtimeDirectory = Path.Combine(outputDirectory, "Runtime");
        RecreateDirectory(dataDirectory);
        RecreateDirectory(assetDirectory);
        if (profile.PackageRuntime)
        {
            RecreateDirectory(runtimeDirectory);
        }

        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(assetDirectory);

        var includedFiles = new List<string>
        {
            "Data/project.pompo.json"
        };

        if (profile.PackageRuntime)
        {
            var publishDiagnostics = await PublishRuntimeAsync(
                profile,
                runtimeDirectory,
                cancellationToken).ConfigureAwait(false);

            if (publishDiagnostics.Count > 0)
            {
                return new BuildResult(false, outputDirectory, publishDiagnostics, null);
            }

            RemoveRuntimeDebugSymbols(runtimeDirectory);
            AddDirectoryFiles(outputDirectory, runtimeDirectory, includedFiles);
        }
        else if (profile.RunSmokeTest)
        {
            return new BuildResult(
                false,
                outputDirectory,
                [new BuildDiagnostic("BUILD005", "Runtime smoke test requires PackageRuntime to be true.")],
                null);
        }

        await CopyProjectFileAsync(projectRoot, dataDirectory, cancellationToken).ConfigureAwait(false);
        CopyAssets(projectRoot, outputDirectory, project, includedFiles);
        await WriteUserScriptAssemblyAsync(
                outputDirectory,
                runtimeDirectory,
                profile.PackageRuntime,
                scriptCompilation.AssemblyBytes,
                includedFiles,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var graph in project.Graphs)
        {
            var ir = _graphCompiler.Compile(graph);
            var irPath = Path.Combine(dataDirectory, $"{ir.GraphId}.pompo-ir.json");
            await AtomicFileWriter.WriteJsonAsync(
                irPath,
                ir,
                ProjectFileService.CreateJsonOptions(),
                cancellationToken).ConfigureAwait(false);
        }

        includedFiles.AddRange(compiledGraphs.Select(name => $"Data/{name}"));
        if (profile.RunSmokeTest)
        {
            var smokeDiagnostics = await new PackagedRuntimeSmokeTester().RunAsync(
                outputDirectory,
                runtimeDirectory,
                compiledGraphs,
                supportedLocales,
                cancellationToken).ConfigureAwait(false);
            if (smokeDiagnostics.Count > 0)
            {
                return new BuildResult(false, outputDirectory, smokeDiagnostics, null);
            }
        }

        var manifest = new BuildArtifactManifest(
            profile.AppName,
            profile.Version,
            profile.Platform,
            profile.PackageRuntime && profile.SelfContained,
            includedFiles,
            compiledGraphs,
            supportedLocales,
            profile.RunSmokeTest ? supportedLocales : []);

        var manifestPath = Path.Combine(outputDirectory, "pompo-build-manifest.json");
        await AtomicFileWriter.WriteJsonAsync(
            manifestPath,
            manifest,
            ProjectFileService.CreateJsonOptions(),
            cancellationToken).ConfigureAwait(false);

        return new BuildResult(true, outputDirectory, [], manifest);
    }

    private sealed record UserScriptBuildResult(byte[]? AssemblyBytes, IReadOnlyList<BuildDiagnostic> Diagnostics);

    private static async Task<UserScriptBuildResult> CompileUserScriptsAsync(
        string projectRoot,
        PompoScriptPermissions permissions,
        CancellationToken cancellationToken)
    {
        var scriptsRoot = Path.Combine(projectRoot, UserScriptsDirectoryName);
        if (!Directory.Exists(scriptsRoot))
        {
            return new UserScriptBuildResult(null, []);
        }

        var diagnostics = new List<BuildDiagnostic>();
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        var projectFullPath = Path.GetFullPath(projectRoot);
        var scriptsFullPath = Path.GetFullPath(scriptsRoot);
        foreach (var file in Directory
                     .EnumerateFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories)
                     .Order(StringComparer.Ordinal))
        {
            var fullPath = Path.GetFullPath(file);
            var relativePath = NormalizeArchivePath(Path.GetRelativePath(projectFullPath, fullPath));
            if (!IsInsideDirectory(fullPath, scriptsFullPath) ||
                !relativePath.StartsWith($"{UserScriptsDirectoryName}/", StringComparison.Ordinal) ||
                !AssetDatabaseService.IsSafeProjectRelativePath(relativePath))
            {
                diagnostics.Add(new BuildDiagnostic(
                    "BUILD011",
                    $"User script path '{relativePath}' is not a safe project-relative script path.",
                    relativePath));
                continue;
            }

            var attributes = File.GetAttributes(fullPath);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                diagnostics.Add(new BuildDiagnostic(
                    "BUILD011",
                    $"User script '{relativePath}' cannot be a symbolic link.",
                    relativePath));
                continue;
            }

            sources[relativePath] = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
        }

        if (diagnostics.Count > 0 || sources.Count == 0)
        {
            return new UserScriptBuildResult(null, diagnostics);
        }

        var result = new UserScriptCompiler().Compile(
            sources,
            new ScriptSecurityOptions(
                permissions.AllowFileSystem,
                permissions.AllowNetwork,
                permissions.AllowProcessExecution));
        if (result.Success)
        {
            return new UserScriptBuildResult(result.AssemblyBytes, []);
        }

        return new UserScriptBuildResult(
            null,
            result.Diagnostics.Select(diagnostic => new BuildDiagnostic(
                    "BUILD011",
                    $"User script compilation failed: {diagnostic}",
                    TryExtractDiagnosticPath(diagnostic)))
                .ToArray());
    }

    private static async Task WriteUserScriptAssemblyAsync(
        string outputDirectory,
        string runtimeDirectory,
        bool packageRuntime,
        byte[]? assemblyBytes,
        ICollection<string> includedFiles,
        CancellationToken cancellationToken)
    {
        if (assemblyBytes is null)
        {
            return;
        }

        var targetDirectory = packageRuntime
            ? runtimeDirectory
            : Path.Combine(outputDirectory, UserScriptsDirectoryName);
        var relativePath = packageRuntime
            ? NormalizeArchivePath(Path.Combine("Runtime", UserScriptsAssemblyName))
            : NormalizeArchivePath(Path.Combine(UserScriptsDirectoryName, UserScriptsAssemblyName));
        Directory.CreateDirectory(targetDirectory);
        var targetPath = Path.Combine(targetDirectory, UserScriptsAssemblyName);
        await File.WriteAllBytesAsync(targetPath, assemblyBytes, cancellationToken).ConfigureAwait(false);
        includedFiles.Add(relativePath);
    }

    private static bool IsInsideDirectory(string fullPath, string directoryFullPath)
    {
        var directory = directoryFullPath.EndsWith(Path.DirectorySeparatorChar)
            ? directoryFullPath
            : $"{directoryFullPath}{Path.DirectorySeparatorChar}";
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return fullPath.StartsWith(directory, comparison);
    }

    private static string? TryExtractDiagnosticPath(string diagnostic)
    {
        var pathEndIndex = diagnostic.IndexOf('(', StringComparison.Ordinal);
        if (pathEndIndex <= 0)
        {
            pathEndIndex = diagnostic.IndexOf(':', StringComparison.Ordinal);
        }

        if (pathEndIndex <= 0)
        {
            return null;
        }

        var candidate = NormalizeArchivePath(diagnostic[..pathEndIndex]);
        return AssetDatabaseService.IsSafeProjectRelativePath(candidate) ? candidate : null;
    }

    private static string[] NormalizeLocales(IEnumerable<string> locales)
    {
        return locales
            .Where(locale => !string.IsNullOrWhiteSpace(locale))
            .Select(locale => locale.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task<IReadOnlyList<BuildDiagnostic>> PublishRuntimeAsync(
        PompoBuildProfile profile,
        string runtimeDirectory,
        CancellationToken cancellationToken)
    {
        var runtimeProject = ResolveRuntimeProjectPath(profile.RuntimeProjectPath);
        if (runtimeProject is null)
        {
            return
            [
                new BuildDiagnostic(
                    "BUILD003",
                    "Could not find Pompo.Runtime.Fna.csproj. Pass RuntimeProjectPath or run the build from the engine repository root.")
            ];
        }

        var rid = GetRuntimeIdentifier(profile.Platform);
        var arguments = new[]
        {
            "publish",
            runtimeProject,
            "-c",
            "Release",
            "-o",
            runtimeDirectory,
            "-r",
            rid,
            "--self-contained",
            profile.SelfContained ? "true" : "false",
            "/p:PublishSingleFile=false"
        };

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode == 0)
        {
            return [];
        }

        var message = string.Join(
            Environment.NewLine,
            new[] { stdout.Trim(), stderr.Trim() }.Where(text => !string.IsNullOrWhiteSpace(text)));
        return [new BuildDiagnostic("BUILD004", $"Runtime publish failed: {message}", runtimeProject)];
    }

    private static string? ResolveRuntimeProjectPath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "Pompo.Runtime.Fna", "Pompo.Runtime.Fna.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }

    private static string GetRuntimeIdentifier(PompoTargetPlatform platform)
    {
        return platform switch
        {
            PompoTargetPlatform.Windows => "win-x64",
            PompoTargetPlatform.MacOS => RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64",
            PompoTargetPlatform.Linux => RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64",
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
        };
    }

    private static void AddDirectoryFiles(string outputDirectory, string directory, ICollection<string> includedFiles)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            includedFiles.Add(NormalizeArchivePath(Path.GetRelativePath(outputDirectory, file)));
        }
    }

    private static string NormalizeArchivePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static void RecreateDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        Directory.CreateDirectory(directory);
    }

    private static async Task CopyProjectFileAsync(
        string projectRoot,
        string dataDirectory,
        CancellationToken cancellationToken)
    {
        var source = ProjectFileService.GetProjectFilePath(projectRoot);
        var target = Path.Combine(dataDirectory, ProjectConstants.ProjectFileName);
        await using var sourceStream = File.OpenRead(source);
        await using var targetStream = File.Create(target);
        await sourceStream.CopyToAsync(targetStream, cancellationToken).ConfigureAwait(false);
    }

    private static void CopyAssets(
        string projectRoot,
        string outputDirectory,
        PompoProjectDocument project,
        ICollection<string> includedFiles)
    {
        foreach (var asset in project.Assets.Assets)
        {
            if (!AssetDatabaseService.IsSafeProjectRelativePath(asset.SourcePath))
            {
                continue;
            }

            var source = Path.Combine(projectRoot, asset.SourcePath);
            var relativeTarget = asset.SourcePath.Replace('\\', '/');
            var target = Path.Combine(outputDirectory, relativeTarget);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
            includedFiles.Add(relativeTarget);
        }
    }

    private static void RemoveRuntimeDebugSymbols(string runtimeDirectory)
    {
        foreach (var file in Directory.EnumerateFiles(runtimeDirectory, "*.pdb", SearchOption.AllDirectories))
        {
            File.Delete(file);
        }
    }
}
