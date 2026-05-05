using System.Security.Cryptography;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Pompo.Core;
using Pompo.Build;
using Pompo.Core.Assets;
using Pompo.Core.Localization;
using Pompo.Core.Project;
using Pompo.Core.Runtime;
using Pompo.VisualScripting;

namespace Pompo.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        try
        {
            return args[0] switch
            {
                "-v" or "--version" or "version" => VersionAsync(args.Skip(1).ToArray()),
                "init" => await InitAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
                "doctor" => await DoctorAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
                "validate" => await ValidateAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
                "build" => await BuildAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
                "release" => await ReleaseAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
                "asset" => await AssetAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
                "profile" => await ProfileAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
                "history" => await HistoryAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
                "localization" => await LocalizationAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
                "docs" => await DocsAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
                "save" => await SaveAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
                _ => UnknownCommand(args[0])
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or InvalidOperationException or CryptographicException)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static int VersionAsync(string[] args)
    {
        var assembly = typeof(Program).Assembly;
        var version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown";
        var runtime = RuntimeInformation.FrameworkDescription;
        var os = RuntimeInformation.OSDescription;
        var architecture = RuntimeInformation.ProcessArchitecture.ToString();

        if (HasFlag(args, "--json"))
        {
            WriteJson(new
            {
                product = "PompoEngine",
                cliVersion = version,
                schemaVersion = ProjectConstants.CurrentSchemaVersion,
                runtime,
                os,
                architecture
            });
            return 0;
        }

        Console.WriteLine($"PompoEngine CLI {version}");
        Console.WriteLine($"schema: {ProjectConstants.CurrentSchemaVersion}");
        Console.WriteLine($"runtime: {runtime}");
        Console.WriteLine($"os: {os}");
        Console.WriteLine($"arch: {architecture}");
        return 0;
    }

    private static async Task<int> InitAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var path = GetOption(args, "--path") ?? Directory.GetCurrentDirectory();
        var name = GetOption(args, "--name") ?? new DirectoryInfo(path).Name;
        var template = GetOption(args, "--template") ?? "minimal";

        if (File.Exists(ProjectFileService.GetProjectFilePath(path)))
        {
            Console.Error.WriteLine($"error: project already exists at '{path}'.");
            return 1;
        }

        var templates = new ProjectTemplateService();
        var project = template.ToLowerInvariant() switch
        {
            "minimal" => await templates.CreateMinimalVisualNovelAsync(path, name).ConfigureAwait(false),
            "sample" => await templates.CreateSampleVisualNovelAsync(path, name).ConfigureAwait(false),
            _ => throw new ArgumentException($"Unknown template '{template}'. Use minimal or sample.")
        };

        if (json)
        {
            WriteJson(new
            {
                projectRoot = path,
                projectFile = ProjectFileService.GetProjectFilePath(path),
                template,
                project
            });
            return 0;
        }

        Console.WriteLine($"created: {ProjectFileService.GetProjectFilePath(path)}");
        Console.WriteLine($"projectId: {project.ProjectId}");
        return 0;
    }

    private static async Task<int> DoctorAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        if (HasFlag(args, "--repository"))
        {
            var root = GetOption(args, "--root") ?? Directory.GetCurrentDirectory();
            var repositoryResult = await new RepositoryDoctorService().InspectAsync(root).ConfigureAwait(false);

            if (json)
            {
                WriteJson(new
                {
                    repositoryRoot = root,
                    healthy = repositoryResult.IsHealthy,
                    diagnostics = repositoryResult.Diagnostics
                });
                return repositoryResult.IsHealthy ? 0 : 1;
            }

            foreach (var diagnostic in repositoryResult.Diagnostics)
            {
                Console.Error.WriteLine(string.IsNullOrWhiteSpace(diagnostic.Path)
                    ? $"{diagnostic.Code}: {diagnostic.Message}"
                    : $"{diagnostic.Code}: {diagnostic.Path}: {diagnostic.Message}");
            }

            if (!repositoryResult.IsHealthy)
            {
                return 1;
            }

            Console.WriteLine($"repository doctor ok: {root}");
            return 0;
        }

        var path = GetOption(args, "--project") ?? Directory.GetCurrentDirectory();
        var result = await new ProjectDoctorService().InspectAsync(path).ConfigureAwait(false);

        if (json)
        {
            WriteJson(new
            {
                projectRoot = path,
                healthy = result.IsHealthy,
                diagnostics = result.Diagnostics
            });
            return result.IsHealthy ? 0 : 1;
        }

        foreach (var diagnostic in result.Diagnostics)
        {
            Console.Error.WriteLine(string.IsNullOrWhiteSpace(diagnostic.Path)
                ? $"{diagnostic.Code}: {diagnostic.Message}"
                : $"{diagnostic.Code}: {diagnostic.Path}: {diagnostic.Message}");
        }

        if (!result.IsHealthy)
        {
            return 1;
        }

        Console.WriteLine($"doctor ok: {path}");
        return 0;
    }

    private static async Task<int> ValidateAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var path = GetOption(args, "--project") ?? Directory.GetCurrentDirectory();
        var project = await new ProjectFileService().LoadAsync(path).ConfigureAwait(false);

        var projectResult = new ProjectValidator().Validate(project, path);
        var assetDiagnostics = await new AssetDatabaseService().ValidateHashesAsync(path, project).ConfigureAwait(false);
        var graphValidator = new GraphValidator();
        var graphDiagnostics = project.Graphs
            .SelectMany(graph => graphValidator.Validate(graph).Diagnostics.Select(diagnostic => (graph.GraphId, Diagnostic: diagnostic)))
            .ToArray();

        if (json)
        {
            var valid = projectResult.IsValid && graphDiagnostics.Length == 0 && assetDiagnostics.Count == 0;
            WriteJson(new
            {
                projectRoot = path,
                valid,
                projectDiagnostics = projectResult.Diagnostics,
                graphDiagnostics = graphDiagnostics.Select(item => new
                {
                    item.GraphId,
                    item.Diagnostic.Code,
                    item.Diagnostic.Message,
                    item.Diagnostic.NodeId,
                    item.Diagnostic.PortId
                }).ToArray(),
                assetDiagnostics
            });
            return valid ? 0 : 1;
        }

        foreach (var diagnostic in projectResult.Diagnostics)
        {
            Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
        }

        foreach (var diagnostic in graphDiagnostics)
        {
            Console.Error.WriteLine($"{diagnostic.Diagnostic.Code}: {diagnostic.GraphId}: {diagnostic.Diagnostic.Message}");
        }

        foreach (var diagnostic in assetDiagnostics)
        {
            Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
        }

        if (!projectResult.IsValid || graphDiagnostics.Length > 0 || assetDiagnostics.Count > 0)
        {
            return 1;
        }

        Console.WriteLine($"valid: {path}");
        return 0;
    }

    private static async Task<int> AssetAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("error: missing asset command. Use list, import, delete, verify, or rehash.");
            return 1;
        }

        return args[0] switch
        {
            "list" => await ListAssetsAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            "import" => await ImportAssetAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            "delete" => await DeleteAssetAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            "verify" => await VerifyAssetsAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            "rehash" => await RehashAssetsAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            _ => UnknownCommand($"asset {args[0]}")
        };
    }

    private static async Task<int> ListAssetsAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var projectPath = GetOption(args, "--project") ?? Directory.GetCurrentDirectory();
        var typeText = GetOption(args, "--type");
        PompoAssetType? type = null;
        if (!string.IsNullOrWhiteSpace(typeText))
        {
            if (!Enum.TryParse<PompoAssetType>(typeText, ignoreCase: true, out var parsedType))
            {
                Console.Error.WriteLine($"error: unsupported asset type '{typeText}'. Use Image, Audio, Font, Script, or Data.");
                return 1;
            }

            type = parsedType;
        }

        var project = await new ProjectFileService().LoadAsync(projectPath).ConfigureAwait(false);
        var assets = project.Assets.Assets
            .Where(asset => type is null || asset.Type == type.Value)
            .OrderBy(asset => asset.Type)
            .ThenBy(asset => asset.AssetId, StringComparer.Ordinal)
            .ToArray();

        if (json)
        {
            WriteJson(new
            {
                projectRoot = projectPath,
                type,
                assets
            });
            return 0;
        }

        foreach (var asset in assets)
        {
            var usage = asset.Usages.Count == 0
                ? "-"
                : string.Join(",", asset.Usages.Select(item => string.IsNullOrWhiteSpace(item.ElementId)
                    ? $"{item.Kind}:{item.DocumentPath}"
                    : $"{item.Kind}:{item.DocumentPath}#{item.ElementId}"));
            Console.WriteLine($"{asset.AssetId}\t{asset.Type}\t{asset.SourcePath}\t{asset.Hash}\t{usage}");
        }

        Console.WriteLine($"assets: {assets.Length}");
        return 0;
    }

    private static async Task<int> ImportAssetAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var projectPath = GetOption(args, "--project") ?? Directory.GetCurrentDirectory();
        var file = GetOption(args, "--file") ?? throw new ArgumentException("Missing --file.");
        var typeText = GetOption(args, "--type") ?? throw new ArgumentException("Missing --type.");
        var assetId = GetOption(args, "--asset-id");

        if (!Enum.TryParse<PompoAssetType>(typeText, ignoreCase: true, out var type))
        {
            Console.Error.WriteLine($"error: unsupported asset type '{typeText}'. Use Image, Audio, Font, Script, or Data.");
            return 1;
        }

        var projectFiles = new ProjectFileService();
        var project = await projectFiles.LoadAsync(projectPath).ConfigureAwait(false);
        var metadata = await new AssetDatabaseService()
            .ImportAsync(projectPath, project, new AssetImportRequest(file, type, assetId))
            .ConfigureAwait(false);
        await projectFiles.SaveAsync(projectPath, project).ConfigureAwait(false);

        if (json)
        {
            WriteJson(new
            {
                projectRoot = projectPath,
                sourceFile = file,
                asset = metadata
            });
            return 0;
        }

        Console.WriteLine($"imported: {metadata.AssetId}");
        Console.WriteLine($"path: {metadata.SourcePath}");
        Console.WriteLine($"hash: {metadata.Hash}");
        return 0;
    }

    private static async Task<int> DeleteAssetAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var projectPath = GetOption(args, "--project") ?? Directory.GetCurrentDirectory();
        var assetId = GetOption(args, "--asset-id") ?? throw new ArgumentException("Missing --asset-id.");
        var keepFile = HasFlag(args, "--keep-file");
        var projectFiles = new ProjectFileService();
        var project = await projectFiles.LoadAsync(projectPath).ConfigureAwait(false);
        new AssetDatabaseService().Delete(projectPath, project, assetId, deleteFile: !keepFile);
        await projectFiles.SaveAsync(projectPath, project).ConfigureAwait(false);
        if (json)
        {
            WriteJson(new
            {
                projectRoot = projectPath,
                assetId,
                fileDeleted = !keepFile
            });
            return 0;
        }

        Console.WriteLine($"deleted: {assetId}");
        Console.WriteLine($"file: {(keepFile ? "kept" : "deleted")}");
        return 0;
    }

    private static async Task<int> VerifyAssetsAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var projectPath = GetOption(args, "--project") ?? Directory.GetCurrentDirectory();
        var project = await new ProjectFileService().LoadAsync(projectPath).ConfigureAwait(false);
        var diagnostics = await new AssetDatabaseService().ValidateHashesAsync(projectPath, project).ConfigureAwait(false);
        if (json)
        {
            WriteJson(new
            {
                projectRoot = projectPath,
                valid = diagnostics.Count == 0,
                assetCount = project.Assets.Assets.Count,
                diagnostics
            });
            return diagnostics.Count == 0 ? 0 : 1;
        }

        foreach (var diagnostic in diagnostics)
        {
            Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
        }

        if (diagnostics.Count > 0)
        {
            return 1;
        }

        Console.WriteLine($"assets valid: {project.Assets.Assets.Count}");
        return 0;
    }

    private static async Task<int> RehashAssetsAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var projectPath = GetOption(args, "--project") ?? Directory.GetCurrentDirectory();
        var projectFiles = new ProjectFileService();
        var project = await projectFiles.LoadAsync(projectPath).ConfigureAwait(false);
        var refreshed = await new AssetDatabaseService().RefreshHashesAsync(projectPath, project).ConfigureAwait(false);
        await projectFiles.SaveAsync(projectPath, project).ConfigureAwait(false);

        if (json)
        {
            WriteJson(new
            {
                projectRoot = projectPath,
                refreshed
            });
            return 0;
        }

        Console.WriteLine($"refreshed: {refreshed}");
        return 0;
    }

    private static async Task<int> BuildAsync(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "verify", StringComparison.Ordinal))
        {
            return await VerifyBuildAsync(args.Skip(1).ToArray()).ConfigureAwait(false);
        }

        var json = HasFlag(args, "--json");
        var projectPath = GetOption(args, "--project") ?? Directory.GetCurrentDirectory();
        var output = GetOption(args, "--output") ?? Path.Combine(projectPath, "Builds");
        var profileFile = GetOption(args, "--profile-file");
        var fileProfile = profileFile is null
            ? null
            : await new BuildProfileFileService().LoadAsync(profileFile).ConfigureAwait(false);
        var platformText = GetOption(args, "--platform") ?? fileProfile?.Platform.ToString() ?? CurrentPlatformName();
        var profileName = GetOption(args, "--profile") ?? fileProfile?.ProfileName ?? "debug";
        var appName = GetOption(args, "--app-name") ??
            fileProfile?.AppName ??
            await ReadProjectNameAsync(projectPath).ConfigureAwait(false);
        var version = GetOption(args, "--version") ?? fileProfile?.Version ?? "0.1.0";
        var runtimeProject = GetOption(args, "--runtime-project") ?? fileProfile?.RuntimeProjectPath;
        var packageRuntime = HasFlag(args, "--skip-runtime-package") ? false : fileProfile?.PackageRuntime ?? true;
        var runSmokeTest = HasFlag(args, "--run-smoke-test") || fileProfile?.RunSmokeTest == true;
        var selfContained = HasFlag(args, "--self-contained")
            ? true
            : HasFlag(args, "--framework-dependent")
                ? false
                : fileProfile?.SelfContained ?? false;

        if (!Enum.TryParse<PompoTargetPlatform>(platformText, ignoreCase: true, out var platform))
        {
            Console.Error.WriteLine($"error: unsupported platform '{platformText}'. Use Windows, MacOS, or Linux.");
            return 1;
        }

        var profile = new PompoBuildProfile(
            profileName,
            platform,
            appName,
            version,
            fileProfile?.IconPath,
            runSmokeTest,
            PackageRuntime: packageRuntime,
            RuntimeProjectPath: runtimeProject,
            SelfContained: selfContained);
        var result = await new PompoBuildPipeline()
            .BuildAsync(
                projectPath,
                profile,
                output)
            .ConfigureAwait(false);

        foreach (var diagnostic in result.Diagnostics)
        {
            if (!json)
            {
                Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
            }
        }

        await RecordBuildHistoryAsync(projectPath, profile, result).ConfigureAwait(false);

        if (json)
        {
            WriteJson(new
            {
                projectRoot = projectPath,
                outputRoot = output,
                success = result.Success,
                outputDirectory = result.OutputDirectory,
                diagnostics = result.Diagnostics,
                manifest = result.Manifest
            });
            return result.Success ? 0 : 1;
        }

        if (!result.Success)
        {
            return 1;
        }

        Console.WriteLine($"built: {result.OutputDirectory}");
        Console.WriteLine($"files: {result.Manifest?.IncludedFiles.Count ?? 0}");
        return 0;
    }

    private static async Task<int> VerifyBuildAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var buildOutput = GetOption(args, "--build") ?? throw new ArgumentException("Missing --build.");
        var options = new BuildOutputVerificationOptions(
            HasFlag(args, "--require-smoke-tested-locales"),
            HasFlag(args, "--require-self-contained"));
        var result = await new BuildOutputVerificationService()
            .VerifyAsync(buildOutput, options)
            .ConfigureAwait(false);

        if (json)
        {
            WriteJson(new
            {
                buildOutput,
                valid = result.IsValid,
                manifest = result.Manifest,
                diagnostics = result.Diagnostics
            });
            return result.IsValid ? 0 : 1;
        }

        foreach (var diagnostic in result.Diagnostics)
        {
            Console.Error.WriteLine(string.IsNullOrWhiteSpace(diagnostic.Path)
                ? $"{diagnostic.Code}: {diagnostic.Message}"
                : $"{diagnostic.Code}: {diagnostic.Path}: {diagnostic.Message}");
        }

        if (!result.IsValid)
        {
            return 1;
        }

        Console.WriteLine($"build valid: {buildOutput}");
        Console.WriteLine($"app: {result.Manifest!.AppName} {result.Manifest.Version} ({result.Manifest.Platform})");
        Console.WriteLine($"self-contained: {result.Manifest.SelfContained}");
        Console.WriteLine($"included files: {result.Manifest.IncludedFiles.Count}");
        Console.WriteLine($"compiled graphs: {result.Manifest.CompiledGraphs.Count}");
        return 0;
    }

    private static async Task RecordBuildHistoryAsync(
        string projectPath,
        PompoBuildProfile profile,
        BuildResult result)
    {
        try
        {
            await new BuildHistoryService()
                .RecordAsync(
                    projectPath,
                    new BuildHistoryEntry(
                        DateTimeOffset.UtcNow,
                        profile.ProfileName,
                        result.Manifest?.Platform ?? profile.Platform,
                        result.OutputDirectory,
                        result.Success,
                        result.Diagnostics.Count,
                        result.Manifest?.AppName ?? profile.AppName,
                        result.Manifest?.Version ?? profile.Version))
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException)
        {
            Console.Error.WriteLine($"warning: build history was not recorded: {ex.Message}");
        }
    }

    private static async Task<string> ReadProjectNameAsync(string projectPath)
    {
        var project = await new ProjectFileService().LoadAsync(projectPath).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(project.ProjectName)
            ? new DirectoryInfo(projectPath).Name
            : project.ProjectName;
    }

    private static async Task<int> ProfileAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("error: missing profile command. Use list, show, save, or delete.");
            return 1;
        }

        return args[0] switch
        {
            "list" => await ListProfilesAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            "show" => await ShowProfileAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            "save" => await SaveProfileAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            "delete" => await DeleteProfileAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            _ => UnknownCommand($"profile {args[0]}")
        };
    }

    private static async Task<int> ListProfilesAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var projectPath = GetOption(args, "--project") ?? Directory.GetCurrentDirectory();
        var profiles = await new BuildProfileFileService().LoadProjectProfilesAsync(projectPath).ConfigureAwait(false);
        if (json)
        {
            WriteJson(new
            {
                projectRoot = projectPath,
                profiles
            });
            return 0;
        }

        foreach (var profile in profiles)
        {
            Console.WriteLine(
                $"{profile.ProfileName}\t{profile.Platform}\t{profile.AppName}\t{profile.Version}\t{FormatProfileRuntime(profile)}");
        }

        Console.WriteLine($"profiles: {profiles.Count}");
        return 0;
    }

    private static async Task<int> ShowProfileAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var projectPath = GetOption(args, "--project") ?? Directory.GetCurrentDirectory();
        var profileName = GetOption(args, "--name") ?? throw new ArgumentException("Missing --name.");
        var path = BuildProfileFileService.GetDefaultProfilePath(projectPath, profileName);
        var profile = await new BuildProfileFileService().LoadAsync(path).ConfigureAwait(false);

        if (json)
        {
            WriteJson(new
            {
                projectRoot = projectPath,
                path,
                profile
            });
            return 0;
        }

        Console.WriteLine($"path: {path}");
        Console.WriteLine($"profileName: {profile.ProfileName}");
        Console.WriteLine($"platform: {profile.Platform}");
        Console.WriteLine($"appName: {profile.AppName}");
        Console.WriteLine($"version: {profile.Version}");
        Console.WriteLine($"iconPath: {profile.IconPath ?? "<none>"}");
        Console.WriteLine($"packageRuntime: {profile.PackageRuntime}");
        Console.WriteLine($"runSmokeTest: {profile.RunSmokeTest}");
        Console.WriteLine($"selfContained: {profile.SelfContained}");
        Console.WriteLine($"runtimeProjectPath: {profile.RuntimeProjectPath ?? "<auto>"}");
        return 0;
    }

    private static async Task<int> SaveProfileAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var projectPath = GetOption(args, "--project") ?? Directory.GetCurrentDirectory();
        var profileName = GetOption(args, "--name") ?? throw new ArgumentException("Missing --name.");
        BuildProfileFileService.ValidateProfileName(profileName);

        var project = await new ProjectFileService().LoadAsync(projectPath).ConfigureAwait(false);
        var service = new BuildProfileFileService();
        var path = BuildProfileFileService.GetDefaultProfilePath(projectPath, profileName);
        var existing = File.Exists(path)
            ? await service.LoadAsync(path).ConfigureAwait(false)
            : null;

        var platformText = GetOption(args, "--platform") ?? existing?.Platform.ToString() ?? CurrentPlatformName();
        if (!Enum.TryParse<PompoTargetPlatform>(platformText, ignoreCase: true, out var platform))
        {
            Console.Error.WriteLine($"error: unsupported platform '{platformText}'. Use Windows, MacOS, or Linux.");
            return 1;
        }

        var appName = GetOption(args, "--app-name") ??
            existing?.AppName ??
            (string.IsNullOrWhiteSpace(project.ProjectName) ? new DirectoryInfo(projectPath).Name : project.ProjectName);
        var version = GetOption(args, "--version") ?? existing?.Version ?? project.EngineVersion;
        var iconPath = GetOption(args, "--icon") ?? existing?.IconPath;
        var runtimeProject = GetOption(args, "--runtime-project") ?? existing?.RuntimeProjectPath;
        var packageRuntime = HasFlag(args, "--data-only")
            ? false
            : HasFlag(args, "--package-runtime")
                ? true
                : existing?.PackageRuntime ?? true;
        var runSmokeTest = HasFlag(args, "--run-smoke-test")
            ? true
            : HasFlag(args, "--skip-smoke-test")
                ? false
                : existing?.RunSmokeTest ?? false;
        var selfContained = HasFlag(args, "--self-contained")
            ? true
            : HasFlag(args, "--framework-dependent")
                ? false
                : existing?.SelfContained ?? false;

        var profile = new PompoBuildProfile(
            profileName.Trim(),
            platform,
            appName.Trim(),
            string.IsNullOrWhiteSpace(version) ? "0.1.0" : version.Trim(),
            string.IsNullOrWhiteSpace(iconPath) ? null : iconPath.Trim(),
            runSmokeTest,
            PackageRuntime: packageRuntime,
            RuntimeProjectPath: string.IsNullOrWhiteSpace(runtimeProject) ? null : runtimeProject.Trim(),
            SelfContained: selfContained);

        await service.SaveProjectProfileAsync(projectPath, profile).ConfigureAwait(false);

        if (json)
        {
            WriteJson(new
            {
                projectRoot = projectPath,
                path,
                profile
            });
            return 0;
        }

        Console.WriteLine($"profile saved: {profile.ProfileName}");
        Console.WriteLine($"path: {path}");
        Console.WriteLine($"platform: {profile.Platform}");
        Console.WriteLine($"runtime: {FormatProfileRuntime(profile)}");
        return 0;
    }

    private static async Task<int> DeleteProfileAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var projectPath = GetOption(args, "--project") ?? Directory.GetCurrentDirectory();
        var profileName = GetOption(args, "--name") ?? throw new ArgumentException("Missing --name.");
        var normalizedProfileName = profileName.Trim();
        await new BuildProfileFileService()
            .DeleteProjectProfileAsync(projectPath, profileName)
            .ConfigureAwait(false);

        if (json)
        {
            WriteJson(new
            {
                projectRoot = projectPath,
                profileName = normalizedProfileName
            });
            return 0;
        }

        Console.WriteLine($"profile deleted: {normalizedProfileName}");
        return 0;
    }

    private static string FormatProfileRuntime(PompoBuildProfile profile)
    {
        var package = profile.PackageRuntime ? "runtime packaged" : "data only";
        var smoke = profile.RunSmokeTest ? "smoke on" : "smoke off";
        var runtime = profile.SelfContained ? "self-contained" : "framework-dependent";
        return $"{package}, {smoke}, {runtime}";
    }

    private static async Task<int> ReleaseAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("error: missing release command. Use package, verify, audit, sign, or verify-signature.");
            return 1;
        }

        return args[0] switch
        {
            "package" => await PackageReleaseAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            "verify" => await VerifyReleaseAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            "audit" => await AuditReleaseAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            "sign" => await SignReleaseAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            "verify-signature" => await VerifyReleaseSignatureAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            _ => UnknownCommand($"release {args[0]}")
        };
    }

    private static async Task<int> PackageReleaseAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var buildOutput = GetOption(args, "--build") ?? throw new ArgumentException("Missing --build.");
        var output = GetOption(args, "--output") ?? Path.Combine(Directory.GetCurrentDirectory(), "Releases");
        var name = GetOption(args, "--name") ?? new DirectoryInfo(buildOutput).Name;

        var manifest = await new ReleasePackageService()
            .PackageAsync(buildOutput, output, name)
            .ConfigureAwait(false);

        if (json)
        {
            WriteJson(new
            {
                buildOutput,
                releaseOutput = output,
                manifest
            });
            return 0;
        }

        Console.WriteLine($"archive: {manifest.ArchivePath}");
        Console.WriteLine($"sha256: {manifest.Sha256}");
        Console.WriteLine($"bytes: {manifest.ArchiveSizeBytes}");
        return 0;
    }

    private static async Task<int> VerifyReleaseAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var manifestPath = GetOption(args, "--manifest") ?? throw new ArgumentException("Missing --manifest.");
        var requireSmokeTestedLocales = HasFlag(args, "--require-smoke-tested-locales");
        var requireSelfContained = HasFlag(args, "--require-self-contained");
        var result = await new ReleasePackageService()
            .VerifyAsync(
                manifestPath,
                new ReleaseVerificationOptions(requireSmokeTestedLocales, requireSelfContained))
            .ConfigureAwait(false);

        if (json)
        {
            WriteJson(new
            {
                manifestPath,
                valid = result.IsValid,
                manifest = result.Manifest,
                diagnostics = result.Diagnostics
            });
            return result.IsValid ? 0 : 1;
        }

        foreach (var diagnostic in result.Diagnostics)
        {
            Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
        }

        if (!result.IsValid)
        {
            return 1;
        }

        Console.WriteLine($"release valid: {result.Manifest!.PackageName}");
        Console.WriteLine($"build: {result.Manifest.BuildAppName} {result.Manifest.BuildVersion} ({result.Manifest.BuildPlatform})");
        Console.WriteLine($"self-contained: {result.Manifest.BuildSelfContained}");
        Console.WriteLine($"locales: {FormatList(result.Manifest.SupportedLocales)}");
        Console.WriteLine($"smoke-tested locales: {FormatList(result.Manifest.SmokeTestedLocales)}");
        Console.WriteLine($"included files: {result.Manifest.IncludedFiles.Count}");
        Console.WriteLine($"compiled graphs: {result.Manifest.CompiledGraphs.Count}");
        Console.WriteLine($"archive: {result.Manifest.ArchivePath}");
        Console.WriteLine($"sha256: {result.Manifest.Sha256}");
        return 0;
    }

    private static async Task<int> AuditReleaseAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var root = GetOption(args, "--root") ?? Directory.GetCurrentDirectory();
        var manifestPath = GetOption(args, "--manifest");
        var requireSmokeTestedLocales = HasFlag(args, "--require-smoke-tested-locales");
        var requireSelfContained = HasFlag(args, "--require-self-contained");
        var result = await new ReleaseAuditService()
            .InspectAsync(
                root,
                manifestPath,
                new ReleaseVerificationOptions(requireSmokeTestedLocales, requireSelfContained))
            .ConfigureAwait(false);

        if (json)
        {
            WriteJson(new
            {
                repositoryRoot = root,
                manifestPath,
                ready = result.IsReady,
                gates = result.Gates
            });
            return result.IsReady ? 0 : 1;
        }

        foreach (var gate in result.Gates)
        {
            var status = gate.Passed ? "PASS" : "FAIL";
            Console.WriteLine(string.IsNullOrWhiteSpace(gate.Path)
                ? $"{status} {gate.Gate}: {gate.Message}"
                : $"{status} {gate.Gate}: {gate.Path}: {gate.Message}");
        }

        return result.IsReady ? 0 : 1;
    }

    private static string FormatList(IReadOnlyList<string> values)
    {
        return values.Count == 0 ? "<none>" : string.Join(", ", values);
    }

    private static async Task<int> SignReleaseAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var manifestPath = GetOption(args, "--manifest") ?? throw new ArgumentException("Missing --manifest.");
        var privateKey = GetOption(args, "--private-key") ?? throw new ArgumentException("Missing --private-key.");
        var result = await new ReleasePackageService()
            .SignAsync(manifestPath, privateKey)
            .ConfigureAwait(false);

        if (json)
        {
            WriteJson(new
            {
                manifestPath,
                signature = result
            });
            return 0;
        }

        Console.WriteLine($"signature: {result.SignaturePath}");
        Console.WriteLine($"algorithm: {result.Algorithm}");
        Console.WriteLine($"bytes: {result.SignatureSizeBytes}");
        return 0;
    }

    private static async Task<int> VerifyReleaseSignatureAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var manifestPath = GetOption(args, "--manifest") ?? throw new ArgumentException("Missing --manifest.");
        var publicKey = GetOption(args, "--public-key") ?? throw new ArgumentException("Missing --public-key.");
        var signature = GetOption(args, "--signature");
        var result = await new ReleasePackageService()
            .VerifySignatureAsync(manifestPath, publicKey, signature)
            .ConfigureAwait(false);

        if (json)
        {
            WriteJson(new
            {
                manifestPath,
                signaturePath = signature,
                valid = result.IsValid,
                diagnostics = result.Diagnostics
            });
            return result.IsValid ? 0 : 1;
        }

        foreach (var diagnostic in result.Diagnostics)
        {
            Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
        }

        if (!result.IsValid)
        {
            return 1;
        }

        Console.WriteLine("release signature valid");
        return 0;
    }

    private static async Task<int> SaveAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("error: missing save command. Use list or delete.");
            return 1;
        }

        return args[0] switch
        {
            "list" => await ListSavesAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            "delete" => await DeleteSaveAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            _ => UnknownCommand($"save {args[0]}")
        };
    }

    private static async Task<int> ListSavesAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var saveRoot = GetOption(args, "--saves") ?? Path.Combine(Directory.GetCurrentDirectory(), "Saves");
        var slots = await new RuntimeSaveStore().ListAsync(saveRoot).ConfigureAwait(false);
        if (json)
        {
            WriteJson(new
            {
                saveRoot,
                slots
            });
            return 0;
        }

        foreach (var slot in slots)
        {
            Console.WriteLine($"{slot.SlotId}\t{slot.DisplayName}\t{slot.GraphId}:{slot.NodeId}\t{slot.SavedAt:O}");
        }

        Console.WriteLine($"slots: {slots.Count}");
        return 0;
    }

    private static async Task<int> HistoryAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("error: missing history command. Use list or clear.");
            return 1;
        }

        return args[0] switch
        {
            "list" => await ListBuildHistoryAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            "clear" => await ClearBuildHistoryAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            _ => UnknownCommand($"history {args[0]}")
        };
    }

    private static async Task<int> ListBuildHistoryAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var projectPath = GetOption(args, "--project") ?? Directory.GetCurrentDirectory();
        var entries = await new BuildHistoryService().LoadAsync(projectPath).ConfigureAwait(false);
        if (json)
        {
            WriteJson(new
            {
                projectRoot = projectPath,
                entries
            });
            return 0;
        }

        foreach (var entry in entries)
        {
            Console.WriteLine(
                $"{entry.BuiltAtUtc:O}\t{entry.ProfileName}\t{entry.Platform}\t{(entry.Success ? "Success" : "Failed")}\t{entry.DiagnosticCount}\t{entry.AppName}\t{entry.Version}\t{entry.OutputDirectory}");
        }

        Console.WriteLine($"history: {entries.Count}");
        return 0;
    }

    private static async Task<int> ClearBuildHistoryAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var projectPath = GetOption(args, "--project") ?? Directory.GetCurrentDirectory();
        await new BuildHistoryService().ClearAsync(projectPath).ConfigureAwait(false);
        if (json)
        {
            WriteJson(new
            {
                projectRoot = projectPath,
                cleared = true
            });
            return 0;
        }

        Console.WriteLine("history cleared");
        return 0;
    }

    private static async Task<int> DeleteSaveAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var saveRoot = GetOption(args, "--saves") ?? Path.Combine(Directory.GetCurrentDirectory(), "Saves");
        var slot = GetOption(args, "--slot") ?? throw new ArgumentException("Missing --slot.");
        await new RuntimeSaveStore().DeleteAsync(saveRoot, slot).ConfigureAwait(false);
        if (json)
        {
            WriteJson(new
            {
                saveRoot,
                slot
            });
            return 0;
        }

        Console.WriteLine($"deleted: {slot}");
        return 0;
    }

    private static async Task<int> LocalizationAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("error: missing localization command. Use report, repair, add-locale, or delete-locale.");
            return 1;
        }

        return args[0] switch
        {
            "report" => await ReportLocalizationAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            "repair" => await RepairLocalizationAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            "add-locale" => await AddLocaleAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            "delete-locale" => await DeleteLocaleAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            _ => UnknownCommand($"localization {args[0]}")
        };
    }

    private static async Task<int> ReportLocalizationAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var projectPath = GetOption(args, "--project") ?? Directory.GetCurrentDirectory();
        var project = await new ProjectFileService().LoadAsync(projectPath).ConfigureAwait(false);
        var report = new LocalizationReportService().Create(project.StringTables, project.SupportedLocales);
        var valid = report.MissingValueCount == 0 && report.UnsupportedValueCount == 0;

        if (json)
        {
            WriteJson(new
            {
                projectRoot = projectPath,
                valid,
                report
            });
            return valid ? 0 : 1;
        }

        Console.WriteLine($"locales: {report.SupportedLocaleCount}");
        Console.WriteLine($"tables: {report.StringTableCount}");
        Console.WriteLine($"entries: {report.EntryCount}");
        Console.WriteLine($"missingValues: {report.MissingValueCount}");
        Console.WriteLine($"unsupportedValues: {report.UnsupportedValueCount}");
        return valid ? 0 : 1;
    }

    private static async Task<int> RepairLocalizationAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var projectPath = GetOption(args, "--project") ?? Directory.GetCurrentDirectory();
        var fallbackLocale = GetOption(args, "--fallback-locale");
        var projectFiles = new ProjectFileService();
        var project = await projectFiles.LoadAsync(projectPath).ConfigureAwait(false);
        var result = new LocalizationRepairService()
            .FillMissingValues(project.StringTables, project.SupportedLocales, fallbackLocale);
        await projectFiles.SaveAsync(projectPath, project).ConfigureAwait(false);

        if (json)
        {
            WriteJson(new
            {
                projectRoot = projectPath,
                fallbackLocale,
                result,
                report = new LocalizationReportService().Create(project.StringTables, project.SupportedLocales)
            });
            return 0;
        }

        Console.WriteLine($"localization repaired: {result.FilledValueCount}");
        return 0;
    }

    private static async Task<int> AddLocaleAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var projectPath = GetOption(args, "--project") ?? Directory.GetCurrentDirectory();
        var locale = GetOption(args, "--locale") ?? throw new ArgumentException("Missing --locale.");
        var normalizedLocale = locale.Trim();
        var projectFiles = new ProjectFileService();
        var project = await projectFiles.LoadAsync(projectPath).ConfigureAwait(false);
        new LocalizationProjectService().AddSupportedLocale(project, locale);
        await projectFiles.SaveAsync(projectPath, project).ConfigureAwait(false);

        if (json)
        {
            WriteJson(new
            {
                projectRoot = projectPath,
                locale = normalizedLocale,
                locales = project.SupportedLocales
            });
            return 0;
        }

        Console.WriteLine($"locale added: {normalizedLocale}");
        Console.WriteLine($"locales: {string.Join(", ", project.SupportedLocales)}");
        return 0;
    }

    private static async Task<int> DeleteLocaleAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var projectPath = GetOption(args, "--project") ?? Directory.GetCurrentDirectory();
        var locale = GetOption(args, "--locale") ?? throw new ArgumentException("Missing --locale.");
        var normalizedLocale = locale.Trim();
        var projectFiles = new ProjectFileService();
        var project = await projectFiles.LoadAsync(projectPath).ConfigureAwait(false);
        new LocalizationProjectService().DeleteSupportedLocale(project, locale);
        await projectFiles.SaveAsync(projectPath, project).ConfigureAwait(false);

        if (json)
        {
            WriteJson(new
            {
                projectRoot = projectPath,
                locale = normalizedLocale,
                locales = project.SupportedLocales
            });
            return 0;
        }

        Console.WriteLine($"locale deleted: {normalizedLocale}");
        Console.WriteLine($"locales: {string.Join(", ", project.SupportedLocales)}");
        return 0;
    }

    private static async Task<int> DocsAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("error: missing docs command. Use site.");
            return 1;
        }

        return args[0] switch
        {
            "site" => await GenerateDocsSiteAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
            _ => UnknownCommand($"docs {args[0]}")
        };
    }

    private static async Task<int> GenerateDocsSiteAsync(string[] args)
    {
        var json = HasFlag(args, "--json");
        var root = GetOption(args, "--root") ?? Directory.GetCurrentDirectory();
        var output = GetOption(args, "--output") ?? Path.Combine(root, "artifacts", "docs-site");
        var manifest = await new DocumentationSiteService()
            .GenerateAsync(root, output)
            .ConfigureAwait(false);

        if (json)
        {
            WriteJson(new
            {
                repositoryRoot = root,
                outputDirectory = output,
                manifest
            });
            return 0;
        }

        Console.WriteLine($"docs site: {Path.Combine(output, "index.html")}");
        Console.WriteLine($"pages: {manifest.Pages.Count}");
        return 0;
    }

    private static string? GetOption(IReadOnlyList<string> args, string name)
    {
        for (var index = 0; index < args.Count; index++)
        {
            if (!string.Equals(args[index], name, StringComparison.Ordinal))
            {
                continue;
            }

            return index + 1 < args.Count ? args[index + 1] : throw new ArgumentException($"Missing value for {name}.");
        }

        return null;
    }

    private static bool HasFlag(IReadOnlyList<string> args, string name)
    {
        return args.Any(arg => string.Equals(arg, name, StringComparison.Ordinal));
    }

    private static void WriteJson<T>(T value)
    {
        Console.WriteLine(JsonSerializer.Serialize(value, ProjectFileService.CreateJsonOptions()));
    }

    private static string CurrentPlatformName()
    {
        if (OperatingSystem.IsWindows())
        {
            return PompoTargetPlatform.Windows.ToString();
        }

        if (OperatingSystem.IsMacOS())
        {
            return PompoTargetPlatform.MacOS.ToString();
        }

        return PompoTargetPlatform.Linux.ToString();
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"error: unknown command '{command}'.");
        PrintHelp();
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            PompoEngine CLI

            Usage:
              pompo init --path <projectDir> --name <projectName> --template <minimal|sample> [--json]
              pompo version [--json]
              pompo doctor --project <projectDir>
              pompo doctor --repository [--root <repoRoot>]
              pompo validate --project <projectDir>
              pompo build --project <projectDir> --output <buildDir> --platform <Windows|MacOS|Linux>
              pompo build --project <projectDir> --profile-file <BuildProfiles/debug.pompo-build.json>
              pompo build verify --build <buildOutputDir> [--require-smoke-tested-locales] [--require-self-contained]
              pompo profile list --project <projectDir>
              pompo profile show --project <projectDir> --name <profileName>
              pompo profile save --project <projectDir> --name <profileName> --platform <Windows|MacOS|Linux> [--json]
              pompo profile delete --project <projectDir> --name <profileName> [--json]
              pompo history list --project <projectDir>
              pompo history clear --project <projectDir> [--json]
              pompo release package --build <buildOutputDir> --output <releaseDir> --name <packageName> [--json]
              pompo release verify --manifest <releaseManifestJson> [--require-smoke-tested-locales] [--require-self-contained] [--json]
              pompo release audit --root <repoRoot> --manifest <releaseManifestJson> [--require-smoke-tested-locales] [--require-self-contained] [--json]
              pompo release sign --manifest <releaseManifestJson> --private-key <privateKeyPem> [--json]
              pompo release verify-signature --manifest <releaseManifestJson> --public-key <publicKeyPem> [--json]
              pompo asset list --project <projectDir> [--type <Image|Audio|Font|Script|Data>] [--json]
              pompo asset import --project <projectDir> --file <sourceFile> --type <Image|Audio|Font|Script|Data> [--json]
              pompo asset delete --project <projectDir> --asset-id <assetId> [--keep-file] [--json]
              pompo asset verify --project <projectDir> [--json]
              pompo asset rehash --project <projectDir> [--json]
              pompo localization report --project <projectDir> [--json]
              pompo localization repair --project <projectDir> [--fallback-locale <locale>] [--json]
              pompo localization add-locale --project <projectDir> --locale <locale> [--json]
              pompo localization delete-locale --project <projectDir> --locale <locale> [--json]
              pompo docs site --root <repoRoot> --output <docsSiteDir> [--json]
              pompo save list --saves <saveDir> [--json]
              pompo save delete --saves <saveDir> --slot <slotId> [--json]

            Output options:
              --json                         Emit machine-readable JSON for init, version, doctor, validate, build, build verify, profile, history list/clear, asset, save list/delete, localization, docs, and release package/verify/signature commands

            Build options:
              --profile-file <path>          Load build settings from JSON profile
              --runtime-project <path>       Explicit Pompo.Runtime.Fna.csproj path
              --run-smoke-test               Execute packaged runtime smoke tests after build
              --self-contained               Publish runtime with the .NET runtime included
              --framework-dependent          Publish runtime that requires an installed .NET runtime
              --skip-runtime-package         Compile data only, without publishing the runtime

            Profile options:
              --package-runtime              Save profile with runtime packaging enabled
              --data-only                    Save profile without runtime publishing
              --run-smoke-test               Save profile with packaged-runtime smoke tests enabled
              --skip-smoke-test              Save profile with packaged-runtime smoke tests disabled
              --self-contained               Save profile as self-contained runtime publish
              --framework-dependent          Save profile as framework-dependent runtime publish

            Release options:
              --require-smoke-tested-locales Fail release verification unless every supported locale was smoke-tested
              --require-self-contained       Fail release verification unless the runtime was published self-contained
            """);
    }
}
