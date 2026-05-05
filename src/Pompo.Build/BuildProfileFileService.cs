using System.Text.Json;
using Pompo.Core.Project;

namespace Pompo.Build;

public sealed class BuildProfileFileService
{
    private const string ProfileSuffix = ".pompo-build.json";

    public async Task SaveAsync(
        string profilePath,
        PompoBuildProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profilePath);
        ValidateProfileName(profile.ProfileName);
        await AtomicFileWriter.WriteJsonAsync(
            profilePath,
            profile,
            ProjectFileService.CreateJsonOptions(),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<PompoBuildProfile> LoadAsync(
        string profilePath,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(profilePath);
        var profile = await JsonSerializer.DeserializeAsync<PompoBuildProfile>(
            stream,
            ProjectFileService.CreateJsonOptions(),
            cancellationToken).ConfigureAwait(false);
        return profile ?? throw new InvalidDataException($"Build profile '{profilePath}' is empty or invalid.");
    }

    public async Task<IReadOnlyList<PompoBuildProfile>> LoadProjectProfilesAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var directory = GetProfileDirectory(projectRoot);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var profiles = new List<PompoBuildProfile>();
        foreach (var path in Directory.EnumerateFiles(directory, $"*{ProfileSuffix}", SearchOption.TopDirectoryOnly)
                     .Order(StringComparer.Ordinal))
        {
            profiles.Add(await LoadAsync(path, cancellationToken).ConfigureAwait(false));
        }

        return profiles
            .OrderBy(profile => profile.ProfileName, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task SaveProjectProfileAsync(
        string projectRoot,
        PompoBuildProfile profile,
        CancellationToken cancellationToken = default)
    {
        await SaveAsync(GetDefaultProfilePath(projectRoot, profile.ProfileName), profile, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task DeleteProjectProfileAsync(
        string projectRoot,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateProfileName(profileName);

        var directory = GetProfileDirectory(projectRoot);
        if (!Directory.Exists(directory))
        {
            throw new InvalidOperationException("BuildProfiles directory does not exist.");
        }

        var profiles = Directory.EnumerateFiles(directory, $"*{ProfileSuffix}", SearchOption.TopDirectoryOnly)
            .ToArray();
        if (profiles.Length <= 1)
        {
            throw new InvalidOperationException("Project must keep at least one build profile.");
        }

        var path = Path.GetFullPath(GetDefaultProfilePath(projectRoot, profileName));
        var root = Path.GetFullPath(directory);
        var relativePath = Path.GetRelativePath(root, path);
        if (relativePath == ".." ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException($"Build profile path for '{profileName}' is outside BuildProfiles.");
        }

        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Build profile '{profileName}' does not exist.");
        }

        File.Delete(path);
        return Task.CompletedTask;
    }

    public static string GetDefaultProfilePath(string projectRoot, string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ValidateProfileName(profileName);
        return Path.Combine(GetProfileDirectory(projectRoot), $"{profileName}{ProfileSuffix}");
    }

    public static string GetProfileDirectory(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        return Path.Combine(projectRoot, "BuildProfiles");
    }

    public static void ValidateProfileName(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        if (!IsValidProfileName(profileName))
        {
            throw new ArgumentException("Build profile name can use letters, numbers, '-', '_', or '.', and must not start or end with '.'.");
        }
    }

    public static bool IsValidProfileName(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName) ||
            !string.Equals(profileName, profileName.Trim(), StringComparison.Ordinal) ||
            profileName is "." or ".." ||
            profileName.StartsWith(".", StringComparison.Ordinal) ||
            profileName.EndsWith(".", StringComparison.Ordinal))
        {
            return false;
        }

        return profileName.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');
    }
}
