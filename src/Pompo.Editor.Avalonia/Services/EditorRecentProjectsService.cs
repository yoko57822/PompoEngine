using System.Text.Json;
using Pompo.Core.Project;

namespace Pompo.Editor.Avalonia.Services;

public sealed record EditorRecentProject(
    string ProjectRoot,
    string ProjectName,
    DateTimeOffset LastOpenedUtc);

internal sealed record EditorRecentProjectsDocument(
    int Version,
    IReadOnlyList<EditorRecentProject> Projects);

public sealed class EditorRecentProjectsService
{
    private const int CurrentVersion = 1;
    private readonly string _settingsPath;
    private readonly int _maxEntries;

    public EditorRecentProjectsService(string? settingsPath = null, int maxEntries = 12)
    {
        _settingsPath = settingsPath ?? GetDefaultSettingsPath();
        _maxEntries = Math.Max(1, maxEntries);
    }

    public async Task<IReadOnlyList<EditorRecentProject>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return [];
        }

        EditorRecentProjectsDocument? document;
        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            document = await JsonSerializer.DeserializeAsync<EditorRecentProjectsDocument>(
                    stream,
                    ProjectFileService.CreateJsonOptions(),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        {
            return [];
        }

        return document is null ? [] : Normalize(document.Projects);
    }

    public async Task<IReadOnlyList<EditorRecentProject>> AddOrUpdateAsync(
        string projectRoot,
        string projectName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);

        var fullRoot = Path.GetFullPath(projectRoot);
        var current = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var projects = current
            .Where(project => !string.Equals(project.ProjectRoot, fullRoot, StringComparison.Ordinal))
            .Prepend(new EditorRecentProject(fullRoot, projectName, DateTimeOffset.UtcNow))
            .Take(_maxEntries)
            .ToArray();
        await SaveAsync(projects, cancellationToken).ConfigureAwait(false);
        return projects;
    }

    public async Task<IReadOnlyList<EditorRecentProject>> RemoveAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var fullRoot = Path.GetFullPath(projectRoot);
        var projects = (await LoadAsync(cancellationToken).ConfigureAwait(false))
            .Where(project => !string.Equals(project.ProjectRoot, fullRoot, StringComparison.Ordinal))
            .ToArray();
        await SaveAsync(projects, cancellationToken).ConfigureAwait(false);
        return projects;
    }

    private async Task SaveAsync(
        IReadOnlyList<EditorRecentProject> projects,
        CancellationToken cancellationToken)
    {
        await AtomicFileWriter.WriteJsonAsync(
                _settingsPath,
                new EditorRecentProjectsDocument(CurrentVersion, Normalize(projects)),
                ProjectFileService.CreateJsonOptions(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private IReadOnlyList<EditorRecentProject> Normalize(IEnumerable<EditorRecentProject> projects)
    {
        return projects
            .Where(project => !string.IsNullOrWhiteSpace(project.ProjectRoot))
            .GroupBy(project => Path.GetFullPath(project.ProjectRoot), StringComparer.Ordinal)
            .Select(group =>
            {
                var latest = group
                    .OrderByDescending(project => project.LastOpenedUtc)
                    .First();
                return latest with { ProjectRoot = Path.GetFullPath(latest.ProjectRoot) };
            })
            .OrderByDescending(project => project.LastOpenedUtc)
            .Take(_maxEntries)
            .ToArray();
    }

    private static string GetDefaultSettingsPath()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(basePath))
        {
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return Path.Combine(basePath, "PompoEngine", "editor-recents.json");
    }
}
