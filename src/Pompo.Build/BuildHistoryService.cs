using System.Text.Json;
using Pompo.Core.Project;

namespace Pompo.Build;

public sealed record BuildHistoryEntry(
    DateTimeOffset BuiltAtUtc,
    string ProfileName,
    PompoTargetPlatform Platform,
    string OutputDirectory,
    bool Success,
    int DiagnosticCount,
    string AppName,
    string Version);

internal sealed record BuildHistoryDocument(
    int Version,
    IReadOnlyList<BuildHistoryEntry> Entries);

public sealed class BuildHistoryService
{
    public const int MaxEntries = 20;
    private const int CurrentVersion = 1;

    public async Task<IReadOnlyList<BuildHistoryEntry>> LoadAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var path = GetHistoryPath(projectRoot);
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var document = await JsonSerializer.DeserializeAsync<BuildHistoryDocument>(
                    stream,
                    ProjectFileService.CreateJsonOptions(),
                    cancellationToken)
                .ConfigureAwait(false);
            return document?.Entries
                .OrderByDescending(entry => entry.BuiltAtUtc)
                .Take(MaxEntries)
                .ToArray() ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<BuildHistoryEntry>> RecordAsync(
        string projectRoot,
        BuildHistoryEntry entry,
        CancellationToken cancellationToken = default)
    {
        var entries = (await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false))
            .Prepend(entry)
            .OrderByDescending(item => item.BuiltAtUtc)
            .Take(MaxEntries)
            .ToArray();
        await AtomicFileWriter.WriteJsonAsync(
                GetHistoryPath(projectRoot),
                new BuildHistoryDocument(CurrentVersion, entries),
                ProjectFileService.CreateJsonOptions(),
                cancellationToken)
            .ConfigureAwait(false);
        return entries;
    }

    public Task ClearAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        return AtomicFileWriter.WriteJsonAsync(
            GetHistoryPath(projectRoot),
            new BuildHistoryDocument(CurrentVersion, []),
            ProjectFileService.CreateJsonOptions(),
            cancellationToken);
    }

    public static string GetHistoryPath(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        return Path.Combine(projectRoot, "Settings", "build-history.pompo.json");
    }
}
