using System.Text.Json;
using System.Text.Json.Nodes;
using Pompo.Core.Assets;
using Pompo.Core.Characters;
using Pompo.Core.Graphs;
using Pompo.Core.Localization;
using Pompo.Core.Project;
using Pompo.Core.Runtime;
using Pompo.Core.Scenes;
using Pompo.Build;
using Pompo.VisualScripting;
using Pompo.Editor.Avalonia.ViewModels;

namespace Pompo.Editor.Avalonia.Services;

public sealed class EditorProjectWorkspaceService
{
    private readonly ProjectFileService _projectFiles = new();
    private readonly ProjectTemplateService _projectTemplates = new();
    private readonly ProjectValidator _projectValidator = new();
    private readonly AssetDatabaseService _assetDatabase = new();
    private readonly GraphValidator _graphValidator = new();
    private readonly BuildProfileFileService _buildProfileFiles = new();
    private readonly PompoBuildPipeline _buildPipeline = new();
    private readonly ReleasePackageService _releasePackages = new();
    private readonly ProjectDoctorService _doctor = new();
    private readonly RuntimeSaveStore _saveStore = new();
    private readonly LocalizationRepairService _localizationRepair = new();
    private readonly LocalizationProjectService _localizationProjects = new();

    public async Task<ProjectWorkspaceState> CreateSampleAsync(
        string projectRoot,
        string projectName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);

        var projectFile = ProjectFileService.GetProjectFilePath(projectRoot);
        if (File.Exists(projectFile))
        {
            throw new InvalidOperationException($"Project file '{projectFile}' already exists.");
        }

        await _projectTemplates.CreateSampleVisualNovelAsync(projectRoot, projectName, cancellationToken)
            .ConfigureAwait(false);
        return await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectWorkspaceState> CreateMinimalAsync(
        string projectRoot,
        string projectName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);

        var projectFile = ProjectFileService.GetProjectFilePath(projectRoot);
        if (File.Exists(projectFile))
        {
            throw new InvalidOperationException($"Project file '{projectFile}' already exists.");
        }

        await _projectTemplates.CreateMinimalVisualNovelAsync(projectRoot, projectName, cancellationToken)
            .ConfigureAwait(false);
        return await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectWorkspaceState> LoadAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectFiles.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        var projectDiagnostics = _projectValidator.Validate(project, projectRoot).Diagnostics
            .Select(diagnostic => new EditorDiagnostic(
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.DocumentPath,
                diagnostic.ElementId))
            .ToList();

        var assetDiagnostics = await _assetDatabase.ValidateHashesAsync(projectRoot, project, cancellationToken)
            .ConfigureAwait(false);
        projectDiagnostics.AddRange(assetDiagnostics.Select(diagnostic => new EditorDiagnostic(
            diagnostic.Code,
            diagnostic.Message,
            diagnostic.DocumentPath,
            diagnostic.ElementId)));

        foreach (var graph in project.Graphs)
        {
            var graphResult = _graphValidator.Validate(graph);
            projectDiagnostics.AddRange(graphResult.Diagnostics.Select(diagnostic => new EditorDiagnostic(
                diagnostic.Code,
                diagnostic.Message,
                graph.GraphId,
                diagnostic.NodeId)));
        }

        var resources = project.Assets.Assets
            .Select(asset => CreateResourceItem(projectRoot, project, asset, projectDiagnostics))
            .ToArray();

        var summary = new ProjectDashboardSummary(
            project.ProjectName,
            projectRoot,
            project.Scenes.Count,
            project.Characters.Count,
            project.Graphs.Count,
            project.Assets.Assets.Count,
            projectDiagnostics.Count,
            resources.Count(resource => resource.IsBroken),
            projectDiagnostics.Count == 0);

        return new ProjectWorkspaceState(project, summary, resources, projectDiagnostics, project.Graphs);
    }

    public async Task<BuildResult> BuildAsync(
        string projectRoot,
        string outputRoot,
        string? profilePath = null,
        bool requireReleaseCandidate = false,
        PompoTargetPlatform? platformOverride = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);

        var project = await _projectFiles.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        var defaultProfileName = requireReleaseCandidate ? "release" : "debug";
        var resolvedProfilePath = profilePath ?? BuildProfileFileService.GetDefaultProfilePath(projectRoot, defaultProfileName);
        var profile = File.Exists(resolvedProfilePath)
            ? await _buildProfileFiles.LoadAsync(resolvedProfilePath, cancellationToken).ConfigureAwait(false)
            : new PompoBuildProfile(defaultProfileName, CurrentPlatform(), project.ProjectName, project.EngineVersion);
        if (requireReleaseCandidate)
        {
            profile = profile with
            {
                RunSmokeTest = true,
                PackageRuntime = true,
                SelfContained = true
            };
        }

        if (platformOverride is not null)
        {
            profile = profile with { Platform = platformOverride.Value };
        }

        return await _buildPipeline.BuildAsync(projectRoot, profile, outputRoot, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ReleaseVerificationResult> PackageReleaseAsync(
        string buildOutputDirectory,
        string releaseOutputDirectory,
        bool requireSmokeTestedLocales = true,
        bool requireSelfContained = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(buildOutputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(releaseOutputDirectory);

        var buildManifest = await LoadBuildManifestAsync(buildOutputDirectory, cancellationToken).ConfigureAwait(false);
        var packageName = CreatePackageName(buildManifest);
        var releaseManifest = await _releasePackages.PackageAsync(
                buildOutputDirectory,
                releaseOutputDirectory,
                packageName,
                cancellationToken)
            .ConfigureAwait(false);

        var manifestPath = Path.Combine(releaseOutputDirectory, $"{releaseManifest.PackageName}.release.json");
        return await _releasePackages.VerifyAsync(
                manifestPath,
                new ReleaseVerificationOptions(requireSmokeTestedLocales, requireSelfContained),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SaveBuildProfileAsync(
        string projectRoot,
        PompoBuildProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        await _buildProfileFiles.SaveProjectProfileAsync(projectRoot, profile, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task DeleteBuildProfileAsync(
        string projectRoot,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        return _buildProfileFiles.DeleteProjectProfileAsync(projectRoot, profileName, cancellationToken);
    }

    public Task<ProjectDoctorResult> RunDoctorAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        return _doctor.InspectAsync(projectRoot, cancellationToken);
    }

    public async Task<ProjectWorkspaceState> SaveRuntimeUiThemeAsync(
        string projectRoot,
        PompoRuntimeUiTheme theme,
        PompoRuntimeUiSkin? skin = null,
        PompoRuntimeUiLayoutSettings? layout = null,
        PompoRuntimeUiAnimationSettings? animation = null,
        PompoRuntimePlaybackSettings? playback = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        var project = await _projectFiles.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        var updated = project with
        {
            RuntimeUiTheme = theme,
            RuntimeUiSkin = skin ?? project.RuntimeUiSkin,
            RuntimeUiLayout = layout ?? project.RuntimeUiLayout,
            RuntimeUiAnimation = animation ?? project.RuntimeUiAnimation,
            RuntimePlayback = playback ?? project.RuntimePlayback
        };
        var appearanceDiagnostics = new ProjectValidator()
            .Validate(updated, projectRoot)
            .Diagnostics
            .Where(IsRuntimeUiAppearanceDiagnostic)
            .ToArray();
        if (appearanceDiagnostics.Length > 0)
        {
            var diagnostic = appearanceDiagnostics[0];
            throw new InvalidOperationException($"{diagnostic.Code}: {diagnostic.Message}");
        }

        await _projectFiles
            .SaveAsync(
                projectRoot,
                updated,
                cancellationToken)
            .ConfigureAwait(false);
        return await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsRuntimeUiAppearanceDiagnostic(ProjectDiagnostic diagnostic)
    {
        return diagnostic.DocumentPath is "runtimeUiTheme" or "runtimeUiSkin" or "runtimeUiLayout" or "runtimeUiAnimation" or "runtimePlayback";
    }

    public async Task<ProjectWorkspaceState> ImportAssetAsync(
        string projectRoot,
        string sourceFile,
        PompoAssetType? assetType = null,
        string? assetId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        var project = await _projectFiles.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        await _assetDatabase.ImportAsync(
                projectRoot,
                project,
                new AssetImportRequest(sourceFile, assetType ?? InferAssetType(sourceFile), assetId),
                cancellationToken)
            .ConfigureAwait(false);
        await _projectFiles.SaveAsync(projectRoot, project, cancellationToken).ConfigureAwait(false);
        return await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectWorkspaceState> DeleteAssetAsync(
        string projectRoot,
        string assetId,
        bool deleteFile = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        var project = await _projectFiles.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        _assetDatabase.Delete(projectRoot, project, assetId, deleteFile);
        await _projectFiles.SaveAsync(projectRoot, project, cancellationToken).ConfigureAwait(false);
        return await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectWorkspaceState> SaveGraphAsync(
        string projectRoot,
        GraphDocument graph,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var project = await _projectFiles.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        var found = false;
        var graphs = project.Graphs.Select(existing =>
        {
            if (!string.Equals(existing.GraphId, graph.GraphId, StringComparison.Ordinal))
            {
                return existing;
            }

            found = true;
            return graph;
        }).ToArray();

        if (!found)
        {
            throw new InvalidOperationException($"Graph '{graph.GraphId}' does not exist in the project.");
        }

        await _projectFiles.SaveAsync(projectRoot, project with { Graphs = graphs.ToList() }, cancellationToken)
            .ConfigureAwait(false);
        return await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectWorkspaceState> AddGraphAsync(
        string projectRoot,
        GraphDocument graph,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var project = await _projectFiles.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        if (project.Graphs.Any(existing => string.Equals(existing.GraphId, graph.GraphId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Graph '{graph.GraphId}' already exists in the project.");
        }

        await _projectFiles.SaveAsync(
                projectRoot,
                project with { Graphs = project.Graphs.Concat([graph]).ToList() },
                cancellationToken)
            .ConfigureAwait(false);
        return await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectWorkspaceState> DeleteGraphAsync(
        string projectRoot,
        string graphId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(graphId);

        var project = await _projectFiles.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        if (project.Graphs.Count <= 1)
        {
            throw new InvalidOperationException("Project must keep at least one graph.");
        }

        if (project.Graphs.All(graph => !string.Equals(graph.GraphId, graphId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Graph '{graphId}' does not exist in the project.");
        }

        var reference = FindGraphReference(project, graphId);
        if (reference is not null)
        {
            throw new InvalidOperationException($"Graph '{graphId}' is referenced by {reference}.");
        }

        var graphs = project.Graphs
            .Where(graph => !string.Equals(graph.GraphId, graphId, StringComparison.Ordinal))
            .ToArray();
        await _projectFiles.SaveAsync(projectRoot, project with { Graphs = graphs.ToList() }, cancellationToken)
            .ConfigureAwait(false);
        return await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectWorkspaceState> RenameGraphAsync(
        string projectRoot,
        string graphId,
        string newGraphId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(graphId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newGraphId);

        if (!IsValidDocumentId(newGraphId))
        {
            throw new InvalidOperationException(
                $"Graph id '{newGraphId}' is invalid. Use letters, numbers, '-', '_', or '.'.");
        }

        if (string.Equals(graphId, newGraphId, StringComparison.Ordinal))
        {
            return await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        }

        var project = await _projectFiles.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        if (project.Graphs.All(graph => !string.Equals(graph.GraphId, graphId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Graph '{graphId}' does not exist in the project.");
        }

        if (project.Graphs.Any(graph => string.Equals(graph.GraphId, newGraphId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Graph '{newGraphId}' already exists in the project.");
        }

        var scenes = project.Scenes
            .Select(scene => string.Equals(scene.StartGraphId, graphId, StringComparison.Ordinal)
                ? scene with { StartGraphId = newGraphId }
                : scene)
            .ToArray();
        var graphs = project.Graphs
            .Select(graph =>
            {
                var nodes = graph.Nodes
                    .Select(node => node.Kind == GraphNodeKind.CallGraph
                        ? UpdateGraphReferenceNode(node, graphId, newGraphId)
                        : node)
                    .ToArray();
                return graph with
                {
                    GraphId = string.Equals(graph.GraphId, graphId, StringComparison.Ordinal)
                        ? newGraphId
                        : graph.GraphId,
                    Nodes = nodes
                };
            })
            .ToArray();

        await _projectFiles.SaveAsync(
                projectRoot,
                project with { Scenes = scenes.ToList(), Graphs = graphs.ToList() },
                cancellationToken)
            .ConfigureAwait(false);
        return await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectWorkspaceState> SaveSceneAsync(
        string projectRoot,
        SceneDefinition scene,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var project = await _projectFiles.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        var found = false;
        var scenes = project.Scenes.Select(existing =>
        {
            if (!string.Equals(existing.SceneId, scene.SceneId, StringComparison.Ordinal))
            {
                return existing;
            }

            found = true;
            return scene;
        }).ToArray();

        if (!found)
        {
            throw new InvalidOperationException($"Scene '{scene.SceneId}' does not exist in the project.");
        }

        await _projectFiles.SaveAsync(projectRoot, project with { Scenes = scenes.ToList() }, cancellationToken)
            .ConfigureAwait(false);
        return await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectWorkspaceState> AddSceneAsync(
        string projectRoot,
        SceneDefinition scene,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var project = await _projectFiles.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        if (project.Scenes.Any(existing => string.Equals(existing.SceneId, scene.SceneId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Scene '{scene.SceneId}' already exists in the project.");
        }

        await _projectFiles.SaveAsync(
                projectRoot,
                project with { Scenes = project.Scenes.Concat([scene]).ToList() },
                cancellationToken)
            .ConfigureAwait(false);
        return await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectWorkspaceState> DeleteSceneAsync(
        string projectRoot,
        string sceneId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneId);

        var project = await _projectFiles.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        if (project.Scenes.Count <= 1)
        {
            throw new InvalidOperationException("Project must keep at least one scene.");
        }

        var scenes = project.Scenes
            .Where(scene => !string.Equals(scene.SceneId, sceneId, StringComparison.Ordinal))
            .ToArray();
        if (scenes.Length == project.Scenes.Count)
        {
            throw new InvalidOperationException($"Scene '{sceneId}' does not exist in the project.");
        }

        await _projectFiles.SaveAsync(projectRoot, project with { Scenes = scenes.ToList() }, cancellationToken)
            .ConfigureAwait(false);
        return await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectWorkspaceState> SaveCharacterAsync(
        string projectRoot,
        CharacterDefinition character,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var project = await _projectFiles.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        var found = false;
        var characters = project.Characters.Select(existing =>
        {
            if (!string.Equals(existing.CharacterId, character.CharacterId, StringComparison.Ordinal))
            {
                return existing;
            }

            found = true;
            return character;
        }).ToArray();

        if (!found)
        {
            throw new InvalidOperationException($"Character '{character.CharacterId}' does not exist in the project.");
        }

        await _projectFiles.SaveAsync(projectRoot, project with { Characters = characters.ToList() }, cancellationToken)
            .ConfigureAwait(false);
        return await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectWorkspaceState> AddCharacterAsync(
        string projectRoot,
        CharacterDefinition character,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var project = await _projectFiles.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        if (project.Characters.Any(existing => string.Equals(existing.CharacterId, character.CharacterId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Character '{character.CharacterId}' already exists in the project.");
        }

        await _projectFiles.SaveAsync(
                projectRoot,
                project with { Characters = project.Characters.Concat([character]).ToList() },
                cancellationToken)
            .ConfigureAwait(false);
        return await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectWorkspaceState> DeleteCharacterAsync(
        string projectRoot,
        string characterId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(characterId);

        var project = await _projectFiles.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        if (project.Scenes.Any(scene => scene.Characters.Any(placement =>
                string.Equals(placement.CharacterId, characterId, StringComparison.Ordinal))))
        {
            throw new InvalidOperationException($"Character '{characterId}' is used by a scene placement.");
        }

        if (project.Graphs.SelectMany(graph => graph.Nodes).Any(node => NodeReferencesCharacter(node, characterId)))
        {
            throw new InvalidOperationException($"Character '{characterId}' is used by a graph node.");
        }

        var characters = project.Characters
            .Where(character => !string.Equals(character.CharacterId, characterId, StringComparison.Ordinal))
            .ToArray();
        if (characters.Length == project.Characters.Count)
        {
            throw new InvalidOperationException($"Character '{characterId}' does not exist in the project.");
        }

        await _projectFiles.SaveAsync(projectRoot, project with { Characters = characters.ToList() }, cancellationToken)
            .ConfigureAwait(false);
        return await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectWorkspaceState> FillMissingLocalizationValuesAsync(
        string projectRoot,
        string? preferredFallbackLocale = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var project = await _projectFiles.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        _localizationRepair.FillMissingValues(project.StringTables, project.SupportedLocales, preferredFallbackLocale);
        await _projectFiles.SaveAsync(projectRoot, project, cancellationToken)
            .ConfigureAwait(false);
        return await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectWorkspaceState> AddSupportedLocaleAsync(
        string projectRoot,
        string locale,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);

        var project = await _projectFiles.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        _localizationProjects.AddSupportedLocale(project, locale);
        await _projectFiles.SaveAsync(projectRoot, project, cancellationToken)
            .ConfigureAwait(false);
        return await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectWorkspaceState> DeleteSupportedLocaleAsync(
        string projectRoot,
        string locale,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);

        var project = await _projectFiles.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        _localizationProjects.DeleteSupportedLocale(project, locale);
        await _projectFiles.SaveAsync(projectRoot, project, cancellationToken)
            .ConfigureAwait(false);
        return await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectWorkspaceState> UpsertLocalizationEntryAsync(
        string projectRoot,
        string tableId,
        string key,
        string locale,
        string value,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);

        var project = await _projectFiles.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        if (!project.SupportedLocales.Contains(locale, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Locale '{locale}' is not supported by the project.");
        }

        var table = project.StringTables.FirstOrDefault(existing =>
            string.Equals(existing.TableId, tableId, StringComparison.Ordinal));
        if (table is null)
        {
            table = new StringTableDocument(tableId, []);
            project.StringTables.Add(table);
        }

        var entry = table.Entries.FirstOrDefault(existing =>
            string.Equals(existing.Key, key, StringComparison.Ordinal));
        if (entry is null)
        {
            entry = new StringTableEntry(key, new Dictionary<string, string>(StringComparer.Ordinal));
            table.Entries.Add(entry);
        }

        entry.Values[locale] = value;
        await _projectFiles.SaveAsync(projectRoot, project, cancellationToken)
            .ConfigureAwait(false);
        return await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectWorkspaceState> DeleteLocalizationEntryAsync(
        string projectRoot,
        string tableId,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var project = await _projectFiles.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        var reference = FindLocalizationReference(project, tableId, key);
        if (reference is not null)
        {
            throw new InvalidOperationException($"Localization '{tableId}:{key}' is referenced by {reference}.");
        }

        var table = project.StringTables.FirstOrDefault(existing =>
            string.Equals(existing.TableId, tableId, StringComparison.Ordinal));
        if (table is null)
        {
            throw new InvalidOperationException($"String table '{tableId}' does not exist in the project.");
        }

        var removed = table.Entries.RemoveAll(entry => string.Equals(entry.Key, key, StringComparison.Ordinal));
        if (removed == 0)
        {
            throw new InvalidOperationException($"String key '{tableId}:{key}' does not exist in the project.");
        }

        if (table.Entries.Count == 0)
        {
            project.StringTables.RemoveAll(existing => string.Equals(existing.TableId, tableId, StringComparison.Ordinal));
        }

        await _projectFiles.SaveAsync(projectRoot, project, cancellationToken)
            .ConfigureAwait(false);
        return await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SaveSlotViewItem>> ListSaveSlotsAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        var slots = await _saveStore.ListAsync(GetSaveRoot(projectRoot), cancellationToken).ConfigureAwait(false);
        return slots
            .Select(slot => new SaveSlotViewItem(slot.SlotId, slot.DisplayName, slot.GraphId, slot.NodeId, slot.SavedAt))
            .ToArray();
    }

    public async Task<IReadOnlyList<SaveSlotViewItem>> DeleteSaveSlotAsync(
        string projectRoot,
        string slotId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        await _saveStore.DeleteAsync(GetSaveRoot(projectRoot), slotId).ConfigureAwait(false);
        return await ListSaveSlotsAsync(projectRoot, cancellationToken).ConfigureAwait(false);
    }

    private static PompoTargetPlatform CurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return PompoTargetPlatform.Windows;
        }

        if (OperatingSystem.IsMacOS())
        {
            return PompoTargetPlatform.MacOS;
        }

        return PompoTargetPlatform.Linux;
    }

    private static async Task<BuildArtifactManifest> LoadBuildManifestAsync(
        string buildOutputDirectory,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(buildOutputDirectory, "pompo-build-manifest.json");
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"Build manifest '{path}' does not exist.");
        }

        await using var stream = File.OpenRead(path);
        return await System.Text.Json.JsonSerializer.DeserializeAsync<BuildArtifactManifest>(
                stream,
                ProjectFileService.CreateJsonOptions(),
                cancellationToken)
            .ConfigureAwait(false) ??
            throw new InvalidDataException($"Build manifest '{path}' is empty.");
    }

    private static string CreatePackageName(BuildArtifactManifest manifest)
    {
        return string.Join(
            "-",
            new[]
            {
                SanitizePackageSegment(manifest.AppName),
                SanitizePackageSegment(manifest.Version),
                manifest.Platform.ToString().ToLowerInvariant()
            }.Where(segment => !string.IsNullOrWhiteSpace(segment)));
    }

    private static string SanitizePackageSegment(string value)
    {
        var sanitized = new string(value
            .Trim()
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.'
                ? character
                : '-')
            .ToArray());
        return string.Join("-", sanitized.Split('-', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool NodeReferencesCharacter(GraphNode node, string characterId)
    {
        return node.Properties.TryGetPropertyValue("characterId", out var value) &&
            value is not null &&
            value.GetValueKind() == JsonValueKind.String &&
            string.Equals(value.GetValue<string>(), characterId, StringComparison.Ordinal);
    }

    private static string? FindGraphReference(PompoProjectDocument project, string graphId)
    {
        foreach (var scene in project.Scenes)
        {
            if (string.Equals(scene.StartGraphId, graphId, StringComparison.Ordinal))
            {
                return $"scene '{scene.SceneId}' start graph";
            }
        }

        foreach (var graph in project.Graphs)
        {
            if (string.Equals(graph.GraphId, graphId, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var node in graph.Nodes)
            {
                if (node.Kind == GraphNodeKind.CallGraph && NodeReferencesGraph(node, graphId))
                {
                    return $"graph '{graph.GraphId}' node '{node.NodeId}'";
                }
            }
        }

        return null;
    }

    private static string? FindLocalizationReference(PompoProjectDocument project, string tableId, string key)
    {
        foreach (var graph in project.Graphs)
        {
            foreach (var node in graph.Nodes)
            {
                if (NodeReferencesLocalization(node, tableId, key))
                {
                    return $"graph '{graph.GraphId}' node '{node.NodeId}'";
                }
            }
        }

        return null;
    }

    private static bool NodeReferencesLocalization(GraphNode node, string tableId, string key)
    {
        if (NodeStringPropertyEquals(node, "textKey", key) &&
            string.Equals(ReadNodeString(node, "tableId") ?? StringTableLocalizer.DefaultTableId, tableId, StringComparison.Ordinal))
        {
            return true;
        }

        if (!node.Properties.TryGetPropertyValue("choices", out var choicesNode) ||
            choicesNode is not JsonArray choices)
        {
            return false;
        }

        foreach (var choice in choices.OfType<JsonObject>())
        {
            if (!ObjectStringPropertyEquals(choice, "textKey", key))
            {
                continue;
            }

            var choiceTableId = ReadObjectString(choice, "tableId") ??
                ReadNodeString(node, "tableId") ??
                StringTableLocalizer.DefaultTableId;
            if (string.Equals(choiceTableId, tableId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool NodeReferencesGraph(GraphNode node, string graphId)
    {
        return NodeStringPropertyEquals(node, "graphId", graphId) ||
            NodeStringPropertyEquals(node, "targetGraphId", graphId);
    }

    private static GraphNode UpdateGraphReferenceNode(GraphNode node, string oldGraphId, string newGraphId)
    {
        JsonObject? properties = null;
        foreach (var key in new[] { "graphId", "targetGraphId" })
        {
            if (!NodeStringPropertyEquals(node, key, oldGraphId))
            {
                continue;
            }

            properties ??= node.Properties.DeepClone().AsObject();
            properties[key] = newGraphId;
        }

        return properties is null ? node : node with { Properties = properties };
    }

    private static bool NodeStringPropertyEquals(GraphNode node, string key, string value)
    {
        return node.Properties.TryGetPropertyValue(key, out var property) &&
            property is not null &&
            property.GetValueKind() == JsonValueKind.String &&
            string.Equals(property.GetValue<string>(), value, StringComparison.Ordinal);
    }

    private static bool ObjectStringPropertyEquals(JsonObject properties, string key, string value)
    {
        return properties.TryGetPropertyValue(key, out var property) &&
            property is not null &&
            property.GetValueKind() == JsonValueKind.String &&
            string.Equals(property.GetValue<string>(), value, StringComparison.Ordinal);
    }

    private static string? ReadNodeString(GraphNode node, string key)
    {
        return node.Properties.TryGetPropertyValue(key, out var property) &&
            property is not null &&
            property.GetValueKind() == JsonValueKind.String
                ? property.GetValue<string>()
                : null;
    }

    private static string? ReadObjectString(JsonObject properties, string key)
    {
        return properties.TryGetPropertyValue(key, out var property) &&
            property is not null &&
            property.GetValueKind() == JsonValueKind.String
                ? property.GetValue<string>()
                : null;
    }

    private static bool IsValidDocumentId(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length > 0 &&
            trimmed is not "." and not ".." &&
            trimmed.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');
    }

    private static PompoAssetType InferAssetType(string sourceFile)
    {
        return Path.GetExtension(sourceFile).ToLowerInvariant() switch
        {
            ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp" => PompoAssetType.Image,
            ".ogg" or ".wav" or ".mp3" or ".flac" => PompoAssetType.Audio,
            ".ttf" or ".otf" => PompoAssetType.Font,
            ".cs" => PompoAssetType.Script,
            _ => PompoAssetType.Data
        };
    }

    private static ResourceItem CreateResourceItem(
        string projectRoot,
        PompoProjectDocument project,
        AssetMetadata asset,
        IReadOnlyList<EditorDiagnostic> diagnostics)
    {
        var missing = !File.Exists(Path.Combine(projectRoot, asset.SourcePath));
        var hashMismatch = diagnostics.Any(diagnostic =>
            string.Equals(diagnostic.ElementId, asset.AssetId, StringComparison.Ordinal) &&
            string.Equals(diagnostic.Code, "ASSET002", StringComparison.Ordinal));

        return new ResourceItem(
            asset.AssetId,
            asset.SourcePath,
            asset.Type,
            asset.Hash,
            AssetDatabaseService.CountReferences(project, asset.AssetId),
            missing,
            hashMismatch);
    }

    private static string GetSaveRoot(string projectRoot)
    {
        return Path.Combine(projectRoot, "Saves");
    }
}
