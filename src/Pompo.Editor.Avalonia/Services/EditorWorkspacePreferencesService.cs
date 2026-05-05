using System.Text.Json;
using Pompo.Core.Project;

namespace Pompo.Editor.Avalonia.Services;

public sealed record EditorWorkspacePreferences(
    string SelectedPresetId,
    bool ProjectPanelVisible,
    bool ScenePanelVisible,
    bool GraphPanelVisible,
    bool InspectorPanelVisible,
    bool ConsolePanelVisible);

internal sealed record EditorWorkspacePreferencesDocument(
    int Version,
    EditorWorkspacePreferences Preferences);

public sealed class EditorWorkspacePreferencesService
{
    private const int CurrentVersion = 1;
    private readonly string _settingsPath;

    public EditorWorkspacePreferencesService(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? GetDefaultSettingsPath();
    }

    public async Task<EditorWorkspacePreferences?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            var document = await JsonSerializer.DeserializeAsync<EditorWorkspacePreferencesDocument>(
                    stream,
                    ProjectFileService.CreateJsonOptions(),
                    cancellationToken)
                .ConfigureAwait(false);
            return document?.Preferences;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        {
            return null;
        }
    }

    public Task SaveAsync(
        EditorWorkspacePreferences preferences,
        CancellationToken cancellationToken = default)
    {
        return AtomicFileWriter.WriteJsonAsync(
            _settingsPath,
            new EditorWorkspacePreferencesDocument(CurrentVersion, preferences),
            ProjectFileService.CreateJsonOptions(),
            cancellationToken);
    }

    private static string GetDefaultSettingsPath()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(basePath))
        {
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return Path.Combine(basePath, "PompoEngine", "editor-workspace.json");
    }
}
