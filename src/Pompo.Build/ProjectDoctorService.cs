using System.Text.Json;
using Pompo.Core;
using Pompo.Core.Assets;
using Pompo.Core.Project;
using Pompo.VisualScripting;

namespace Pompo.Build;

public sealed record ProjectDoctorDiagnostic(string Code, string Message, string? Path = null);

public sealed record ProjectDoctorResult(IReadOnlyList<ProjectDoctorDiagnostic> Diagnostics)
{
    public bool IsHealthy => Diagnostics.Count == 0;
}

public sealed class ProjectDoctorService
{
    private const string ProfileSuffix = ".pompo-build.json";
    private readonly ProjectFileService _projectFiles = new();
    private readonly BuildProfileFileService _profileFiles = new();

    public async Task<ProjectDoctorResult> InspectAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var diagnostics = new List<ProjectDoctorDiagnostic>();
        var projectFile = ProjectFileService.GetProjectFilePath(projectRoot);
        if (!File.Exists(projectFile))
        {
            diagnostics.Add(new ProjectDoctorDiagnostic(
                "DOCTOR001",
                $"Project file '{ProjectConstants.ProjectFileName}' does not exist.",
                ProjectConstants.ProjectFileName));
            return new ProjectDoctorResult(diagnostics);
        }

        ValidateRequiredFolders(projectRoot, diagnostics);

        PompoProjectDocument project;
        try
        {
            project = await _projectFiles.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            diagnostics.Add(new ProjectDoctorDiagnostic(
                "DOCTOR003",
                $"Project file could not be loaded: {ex.Message}",
                ProjectConstants.ProjectFileName));
            return new ProjectDoctorResult(diagnostics);
        }

        ValidateProject(projectRoot, project, diagnostics);
        await ValidateAssetsAsync(projectRoot, project, diagnostics, cancellationToken).ConfigureAwait(false);
        ValidateGraphs(project, diagnostics);
        await ValidateBuildProfilesAsync(projectRoot, diagnostics, cancellationToken).ConfigureAwait(false);

        return new ProjectDoctorResult(diagnostics);
    }

    private static void ValidateRequiredFolders(
        string projectRoot,
        ICollection<ProjectDoctorDiagnostic> diagnostics)
    {
        foreach (var folder in ProjectConstants.RequiredFolders)
        {
            if (!Directory.Exists(Path.Combine(projectRoot, folder)))
            {
                diagnostics.Add(new ProjectDoctorDiagnostic(
                    "DOCTOR002",
                    $"Required folder '{folder}' does not exist.",
                    folder));
            }
        }
    }

    private static void ValidateProject(
        string projectRoot,
        PompoProjectDocument project,
        ICollection<ProjectDoctorDiagnostic> diagnostics)
    {
        var result = new ProjectValidator().Validate(project, projectRoot);
        foreach (var diagnostic in result.Diagnostics)
        {
            diagnostics.Add(new ProjectDoctorDiagnostic(
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.DocumentPath ?? diagnostic.ElementId));
        }
    }

    private static async Task ValidateAssetsAsync(
        string projectRoot,
        PompoProjectDocument project,
        ICollection<ProjectDoctorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var result = await new AssetDatabaseService()
            .ValidateHashesAsync(projectRoot, project, cancellationToken)
            .ConfigureAwait(false);

        foreach (var diagnostic in result)
        {
            diagnostics.Add(new ProjectDoctorDiagnostic(
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.DocumentPath ?? diagnostic.ElementId));
        }
    }

    private static void ValidateGraphs(
        PompoProjectDocument project,
        ICollection<ProjectDoctorDiagnostic> diagnostics)
    {
        var graphValidator = new GraphValidator();
        foreach (var graph in project.Graphs)
        {
            foreach (var diagnostic in graphValidator.Validate(graph).Diagnostics)
            {
                diagnostics.Add(new ProjectDoctorDiagnostic(
                    diagnostic.Code,
                    $"{graph.GraphId}: {diagnostic.Message}",
                    graph.GraphId));
            }
        }
    }

    private async Task ValidateBuildProfilesAsync(
        string projectRoot,
        ICollection<ProjectDoctorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        await ValidateBuildProfileAsync(projectRoot, "debug", diagnostics, cancellationToken).ConfigureAwait(false);
        var release = await ValidateBuildProfileAsync(projectRoot, "release", diagnostics, cancellationToken).ConfigureAwait(false);
        await ValidateAdditionalBuildProfilesAsync(projectRoot, diagnostics, cancellationToken).ConfigureAwait(false);
        if (release is null)
        {
            return;
        }

        var releasePath = BuildProfileFileService.GetDefaultProfilePath(projectRoot, "release");
        if (!release.PackageRuntime)
        {
            diagnostics.Add(new ProjectDoctorDiagnostic(
                "DOCTOR006",
                "Release build profile must package the runtime.",
                releasePath));
        }

        if (!release.RunSmokeTest)
        {
            diagnostics.Add(new ProjectDoctorDiagnostic(
                "DOCTOR007",
                "Release build profile must run packaged-runtime smoke tests.",
                releasePath));
        }

        if (!release.SelfContained)
        {
            diagnostics.Add(new ProjectDoctorDiagnostic(
                "DOCTOR008",
                "Release build profile must publish a self-contained runtime.",
                releasePath));
        }
    }

    private async Task<PompoBuildProfile?> ValidateBuildProfileAsync(
        string projectRoot,
        string profileName,
        ICollection<ProjectDoctorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var path = BuildProfileFileService.GetDefaultProfilePath(projectRoot, profileName);
        if (!File.Exists(path))
        {
            diagnostics.Add(new ProjectDoctorDiagnostic(
                "DOCTOR004",
                $"Build profile '{profileName}' does not exist.",
                path));
            return null;
        }

        try
        {
            var profile = await _profileFiles.LoadAsync(path, cancellationToken).ConfigureAwait(false);
            ValidateLoadedBuildProfile(projectRoot, profile, profileName, path, diagnostics);
            return profile;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            diagnostics.Add(new ProjectDoctorDiagnostic(
                "DOCTOR005",
                $"Build profile '{profileName}' could not be loaded: {ex.Message}",
                path));
            return null;
        }
    }

    private async Task ValidateAdditionalBuildProfilesAsync(
        string projectRoot,
        ICollection<ProjectDoctorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var directory = BuildProfileFileService.GetProfileDirectory(projectRoot);
        if (!Directory.Exists(directory))
        {
            return;
        }

        var requiredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(BuildProfileFileService.GetDefaultProfilePath(projectRoot, "debug")),
            Path.GetFullPath(BuildProfileFileService.GetDefaultProfilePath(projectRoot, "release"))
        };

        foreach (var path in Directory.EnumerateFiles(directory, $"*{ProfileSuffix}", SearchOption.TopDirectoryOnly)
                     .Order(StringComparer.Ordinal))
        {
            var fullPath = Path.GetFullPath(path);
            if (requiredPaths.Contains(fullPath))
            {
                continue;
            }

            var fileName = Path.GetFileName(path);
            var profileName = fileName[..^ProfileSuffix.Length];
            if (!IsValidProfileName(profileName))
            {
                diagnostics.Add(new ProjectDoctorDiagnostic(
                    "DOCTOR009",
                    $"Build profile file '{fileName}' must use a safe profile name.",
                    path));
                continue;
            }

            try
            {
                var profile = await _profileFiles.LoadAsync(path, cancellationToken).ConfigureAwait(false);
                ValidateLoadedBuildProfile(projectRoot, profile, profileName, path, diagnostics);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
            {
                diagnostics.Add(new ProjectDoctorDiagnostic(
                    "DOCTOR005",
                    $"Build profile '{profileName}' could not be loaded: {ex.Message}",
                    path));
            }
        }
    }

    private static void ValidateLoadedBuildProfile(
        string projectRoot,
        PompoBuildProfile profile,
        string expectedProfileName,
        string path,
        ICollection<ProjectDoctorDiagnostic> diagnostics)
    {
        if (!IsValidProfileName(profile.ProfileName))
        {
            diagnostics.Add(new ProjectDoctorDiagnostic(
                "DOCTOR010",
                $"Build profile '{expectedProfileName}' contains an invalid profileName '{profile.ProfileName}'.",
                path));
            return;
        }

        if (!string.Equals(profile.ProfileName, expectedProfileName, StringComparison.Ordinal))
        {
            diagnostics.Add(new ProjectDoctorDiagnostic(
                "DOCTOR011",
                $"Build profile file for '{expectedProfileName}' contains profileName '{profile.ProfileName}'.",
                path));
        }

        if (string.IsNullOrWhiteSpace(profile.AppName))
        {
            diagnostics.Add(new ProjectDoctorDiagnostic(
                "DOCTOR012",
                $"Build profile '{expectedProfileName}' must define a non-empty appName.",
                path));
        }

        if (string.IsNullOrWhiteSpace(profile.Version))
        {
            diagnostics.Add(new ProjectDoctorDiagnostic(
                "DOCTOR013",
                $"Build profile '{expectedProfileName}' must define a non-empty version.",
                path));
        }

        if (!string.IsNullOrWhiteSpace(profile.IconPath) &&
            !FileExistsFromProjectOrWorkingDirectory(projectRoot, profile.IconPath))
        {
            diagnostics.Add(new ProjectDoctorDiagnostic(
                "DOCTOR014",
                $"Build profile '{expectedProfileName}' iconPath '{profile.IconPath}' does not exist.",
                path));
        }

        if (profile.PackageRuntime &&
            !string.IsNullOrWhiteSpace(profile.RuntimeProjectPath) &&
            !FileExistsFromProjectOrWorkingDirectory(projectRoot, profile.RuntimeProjectPath))
        {
            diagnostics.Add(new ProjectDoctorDiagnostic(
                "DOCTOR015",
                $"Build profile '{expectedProfileName}' runtimeProjectPath '{profile.RuntimeProjectPath}' does not exist.",
                path));
        }
    }

    private static bool FileExistsFromProjectOrWorkingDirectory(string projectRoot, string path)
    {
        if (File.Exists(path))
        {
            return true;
        }

        return !Path.IsPathRooted(path) && File.Exists(Path.Combine(projectRoot, path));
    }

    private static bool IsValidProfileName(string profileName)
    {
        return BuildProfileFileService.IsValidProfileName(profileName);
    }
}
