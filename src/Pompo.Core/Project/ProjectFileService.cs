using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pompo.Core.Project;

public sealed class ProjectFileService
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly ProjectMigrationService _migrationService = new();

    public async Task<PompoProjectDocument> CreateAsync(
        string projectRoot,
        string projectName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);

        Directory.CreateDirectory(projectRoot);
        EnsureRequiredFolders(projectRoot);

        var document = new PompoProjectDocument
        {
            ProjectName = projectName
        };

        await SaveAsync(projectRoot, document, cancellationToken).ConfigureAwait(false);
        return _migrationService.Migrate(document).Document;
    }

    public async Task<PompoProjectDocument> LoadAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var path = GetProjectFilePath(projectRoot);
        await using var stream = File.OpenRead(path);
        var document = await JsonSerializer.DeserializeAsync<PompoProjectDocument>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        if (document is null)
        {
            throw new InvalidDataException($"Project file '{path}' is empty or invalid.");
        }

        return _migrationService.Migrate(document).Document;
    }

    public async Task SaveAsync(
        string projectRoot,
        PompoProjectDocument document,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(projectRoot);
        EnsureRequiredFolders(projectRoot);

        var migrated = _migrationService.Migrate(document).Document;
        await AtomicFileWriter.WriteJsonAsync(
            GetProjectFilePath(projectRoot),
            migrated,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);
    }

    public static string GetProjectFilePath(string projectRoot)
    {
        return Path.Combine(projectRoot, ProjectConstants.ProjectFileName);
    }

    public static void EnsureRequiredFolders(string projectRoot)
    {
        foreach (var folder in ProjectConstants.RequiredFolders)
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, folder));
        }
    }

    public static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
