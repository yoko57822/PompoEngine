using System.Text.Json;
using System.Text.Json.Nodes;
using Pompo.Core;
using Pompo.Core.Assets;
using Pompo.Core.Graphs;
using Pompo.Core.Localization;
using Pompo.Core.Project;
using Pompo.Core.Runtime;
using Pompo.Core.Scenes;
using Pompo.Build;
using Pompo.Editor.Avalonia.Services;
using Pompo.Editor.Avalonia.ViewModels;
using Pompo.VisualScripting;

namespace Pompo.Tests;

public sealed class EditorWorkspaceViewModelTests
{
    [Fact]
    public async Task LoadAsync_SummarizesProjectResourcesAndDiagnostics()
    {
        var root = CreateTempDirectory();
        var source = Path.Combine(root, "source.png");
        await File.WriteAllTextAsync(source, "asset");
        var project = await new ProjectTemplateService().CreateSampleVisualNovelAsync(root, "Editor Sample");
        var asset = await new AssetDatabaseService().ImportAsync(
            root,
            project,
            new AssetImportRequest(source, PompoAssetType.Image, "bg-main"));
        await new ProjectFileService().SaveAsync(root, project);

        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);

        Assert.NotNull(viewModel.Summary);
        Assert.Equal("Editor Sample", viewModel.Summary.ProjectName);
        Assert.Equal(3, viewModel.Summary.SceneCount);
        Assert.Equal(2, viewModel.Summary.CharacterCount);
        Assert.Equal(4, viewModel.Summary.GraphCount);
        Assert.Equal(8, viewModel.Summary.AssetCount);
        Assert.True(viewModel.Summary.IsValid);
        Assert.Empty(viewModel.Diagnostics);
        Assert.Contains(viewModel.ResourceBrowser.Resources, resource => resource.AssetId == asset.AssetId);
        Assert.False(viewModel.ResourceBrowser.Resources.Single(resource => resource.AssetId == "bg-intro").IsUnused);
        Assert.True(viewModel.ResourceBrowser.Resources.Single(resource => resource.AssetId == "bg-main").IsUnused);
        Assert.Contains("Audio", viewModel.ResourceBrowser.TypeFilterOptions);
        Assert.NotNull(viewModel.ResourceBrowser.SelectedResource);

        viewModel.ResourceBrowser.Query = "bg";
        Assert.Equal(4, viewModel.ResourceBrowser.FilteredResources.Count);
        viewModel.ResourceBrowser.SelectedTypeFilter = "Audio";
        Assert.Equal(PompoAssetType.Audio, viewModel.ResourceBrowser.TypeFilter);
        Assert.Empty(viewModel.ResourceBrowser.FilteredResources);
        Assert.Null(viewModel.ResourceBrowser.SelectedResource);
        viewModel.ResourceBrowser.Query = string.Empty;
        Assert.Equal(3, viewModel.ResourceBrowser.FilteredResources.Count);
        Assert.Equal(PompoAssetType.Audio, viewModel.ResourceBrowser.SelectedResource!.Type);
    }

    [Fact]
    public async Task LoadAsync_ReportsBrokenAssetsForResourceBrowserFilters()
    {
        var root = CreateTempDirectory();
        var project = new PompoProjectDocument
        {
            ProjectName = "Broken",
            Assets = new AssetDatabase
            {
                Assets =
                [
                    new AssetMetadata(
                        "missing",
                        "Assets/Images/missing.png",
                        PompoAssetType.Image,
                        new AssetImportOptions(),
                        "hash",
                        [])
                ]
            }
        };
        await new ProjectFileService().SaveAsync(root, project);

        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);
        viewModel.ResourceBrowser.ShowOnlyBroken = true;

        Assert.False(viewModel.Summary!.IsValid);
        Assert.Single(viewModel.ResourceBrowser.FilteredResources);
        Assert.True(viewModel.ResourceBrowser.FilteredResources[0].IsBroken);
        Assert.Contains(viewModel.Diagnostics, diagnostic => diagnostic.Code is "POMPO010" or "ASSET001");
    }

    [Fact]
    public async Task CreateSampleProjectAsync_CreatesAndLoadsWorkspace()
    {
        var root = CreateTempDirectory();
        var viewModel = new ProjectWorkspaceViewModel();

        await viewModel.CreateSampleProjectAsync(root, "Editor Created");

        Assert.NotNull(viewModel.Summary);
        Assert.Equal("Editor Created", viewModel.Summary.ProjectName);
        Assert.Equal(root, viewModel.ProjectRoot);
        Assert.Equal(3, viewModel.Summary.SceneCount);
        Assert.Contains("Created sample project", viewModel.StatusMessage);
        Assert.True(File.Exists(Path.Combine(root, ProjectConstants.ProjectFileName)));
        Assert.True(File.Exists(BuildProfileFileService.GetDefaultProfilePath(root, "debug")));
    }

    [Fact]
    public async Task CreateMinimalProjectAsync_CreatesSmallTemplateWorkspace()
    {
        var root = CreateTempDirectory();
        var viewModel = new ProjectWorkspaceViewModel();

        await viewModel.CreateMinimalProjectAsync(root, "Editor Minimal");

        Assert.NotNull(viewModel.Summary);
        Assert.Equal("Editor Minimal", viewModel.Summary.ProjectName);
        Assert.Equal(1, viewModel.Summary.SceneCount);
        Assert.Equal(1, viewModel.Summary.GraphCount);
        Assert.Equal(0, viewModel.Summary.AssetCount);
        Assert.Contains("Created minimal project", viewModel.StatusMessage);
        Assert.True(File.Exists(Path.Combine(root, ProjectConstants.ProjectFileName)));
        Assert.True(File.Exists(BuildProfileFileService.GetDefaultProfilePath(root, "release")));
    }

    [Fact]
    public async Task SaveRuntimeThemeAsync_PersistsProjectThemeFields()
    {
        var root = CreateTempDirectory();
        var project = await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Theme Editor");
        var dialogueSkin = Path.Combine(root, "dialogue.png");
        var selectedSkin = Path.Combine(root, "selected.png");
        var disabledSkin = Path.Combine(root, "disabled.png");
        await File.WriteAllTextAsync(dialogueSkin, "dialogue-skin");
        await File.WriteAllTextAsync(selectedSkin, "selected-skin");
        await File.WriteAllTextAsync(disabledSkin, "disabled-skin");
        await new AssetDatabaseService().ImportAsync(
            root,
            project,
            new AssetImportRequest(dialogueSkin, PompoAssetType.Image, "ui-dialogue"));
        await new AssetDatabaseService().ImportAsync(
            root,
            project,
            new AssetImportRequest(selectedSkin, PompoAssetType.Image, "ui-choice-selected"));
        await new AssetDatabaseService().ImportAsync(
            root,
            project,
            new AssetImportRequest(disabledSkin, PompoAssetType.Image, "ui-choice-disabled"));
        await new ProjectFileService().SaveAsync(root, project);
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);

        viewModel.RuntimeThemeCanvasClear = "#102030";
        viewModel.RuntimeThemeDialogueBackground = "#11223344";
        viewModel.RuntimeThemeAccentText = "#ABCDEF";
        viewModel.RuntimeSkinDialogueBoxAssetId = "ui-dialogue";
        viewModel.RuntimeSkinChoiceSelectedBoxAssetId = " ui-choice-selected ";
        viewModel.RuntimeSkinChoiceDisabledBoxAssetId = "ui-choice-disabled";
        viewModel.RuntimeLayoutDialogueTextBoxX = "100";
        viewModel.RuntimeLayoutDialogueTextBoxY = "700";
        viewModel.RuntimeLayoutDialogueTextBoxWidth = "1200";
        viewModel.RuntimeLayoutDialogueTextBoxHeight = "160";
        viewModel.RuntimeLayoutChoiceBoxSpacing = "20";
        viewModel.RuntimeAnimationEnabled = true;
        viewModel.RuntimeAnimationPanelFadeMilliseconds = "240";
        viewModel.RuntimeAnimationChoicePulseMilliseconds = "1000";
        viewModel.RuntimeAnimationChoicePulseStrength = "0.25";
        viewModel.RuntimeAnimationTextRevealCharactersPerSecond = "60";
        viewModel.RuntimePlaybackAutoForwardDelayMilliseconds = "1500";
        viewModel.RuntimePlaybackSkipIntervalMilliseconds = "40";
        await viewModel.SaveRuntimeThemeAsync();

        var savedProject = await new ProjectFileService().LoadAsync(root);
        Assert.Equal("#102030", savedProject.RuntimeUiTheme.CanvasClear);
        Assert.Equal("#11223344", savedProject.RuntimeUiTheme.DialogueBackground);
        Assert.Equal("#ABCDEF", savedProject.RuntimeUiTheme.AccentText);
        Assert.Equal("ui-dialogue", savedProject.RuntimeUiSkin.DialogueBox?.AssetId);
        Assert.Equal("ui-choice-selected", savedProject.RuntimeUiSkin.ChoiceSelectedBox?.AssetId);
        Assert.Equal("ui-choice-disabled", savedProject.RuntimeUiSkin.ChoiceDisabledBox?.AssetId);
        Assert.Equal(PompoAssetType.Image, savedProject.RuntimeUiSkin.DialogueBox?.Type);
        Assert.Equal(new PompoRuntimeUiRect(100, 700, 1200, 160), savedProject.RuntimeUiLayout.DialogueTextBox);
        Assert.Equal(20, savedProject.RuntimeUiLayout.ChoiceBoxSpacing);
        Assert.Equal(240, savedProject.RuntimeUiAnimation.PanelFadeMilliseconds);
        Assert.Equal(1000, savedProject.RuntimeUiAnimation.ChoicePulseMilliseconds);
        Assert.Equal(0.25f, savedProject.RuntimeUiAnimation.ChoicePulseStrength);
        Assert.Equal(60, savedProject.RuntimeUiAnimation.TextRevealCharactersPerSecond);
        Assert.Equal(1500, savedProject.RuntimePlayback.AutoForwardDelayMilliseconds);
        Assert.Equal(40, savedProject.RuntimePlayback.SkipIntervalMilliseconds);
        Assert.Equal("#102030", viewModel.RuntimeThemeCanvasClear);
        Assert.Contains("Saved runtime UI theme, skin, layout, animation, and playback", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ApplyRuntimeAnimationPreset_UpdatesFieldsAndPersists()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Animation Presets");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);

        viewModel.ApplyRuntimeAnimationPreset("cinematic");
        await viewModel.SaveRuntimeThemeAsync();

        var savedProject = await new ProjectFileService().LoadAsync(root);
        Assert.True(savedProject.RuntimeUiAnimation.Enabled);
        Assert.Equal(320, savedProject.RuntimeUiAnimation.PanelFadeMilliseconds);
        Assert.Equal(1200, savedProject.RuntimeUiAnimation.ChoicePulseMilliseconds);
        Assert.Equal(0.16f, savedProject.RuntimeUiAnimation.ChoicePulseStrength);
        Assert.Equal(32, savedProject.RuntimeUiAnimation.TextRevealCharactersPerSecond);
        Assert.Equal(1800, savedProject.RuntimePlayback.AutoForwardDelayMilliseconds);
        Assert.Equal(120, savedProject.RuntimePlayback.SkipIntervalMilliseconds);
        Assert.Contains("Cinematic", viewModel.RuntimeAnimationPresetSummary);
    }

    [Fact]
    public void ApplyRuntimeAnimationPreset_RejectsUnknownPresetWithoutChangingFields()
    {
        var viewModel = new ProjectWorkspaceViewModel();
        var originalFade = viewModel.RuntimeAnimationPanelFadeMilliseconds;

        viewModel.ApplyRuntimeAnimationPreset("missing");

        Assert.Equal(originalFade, viewModel.RuntimeAnimationPanelFadeMilliseconds);
        Assert.Contains("was not found", viewModel.StatusMessage);
    }

    [Fact]
    public async Task SaveRuntimeThemeAsync_RejectsInvalidRuntimeLayoutNumbers()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Theme Editor");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);

        viewModel.RuntimeLayoutChoiceBoxWidth = "wide";

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            viewModel.SaveRuntimeThemeAsync());

        Assert.Contains("Runtime UI layout field", exception.Message);
        Assert.Contains("Runtime UI layout field", viewModel.StatusMessage);
    }

    [Fact]
    public async Task SaveRuntimeThemeAsync_RejectsInvalidRuntimeAppearanceBeforeSaving()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Theme Editor");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);

        viewModel.RuntimeThemeCanvasClear = "not-a-color";

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            viewModel.SaveRuntimeThemeAsync());
        var project = await new ProjectFileService().LoadAsync(root);

        Assert.Contains("POMPO038", exception.Message);
        Assert.Equal(new PompoRuntimeUiTheme().CanvasClear, project.RuntimeUiTheme.CanvasClear);
    }

    [Fact]
    public async Task ResetRuntimeLayoutFields_RestoresDefaultRuntimeLayoutValues()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Theme Editor");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);

        viewModel.RuntimeLayoutDialogueTextBoxX = "333";
        viewModel.RuntimeLayoutChoiceBoxSpacing = "99";
        viewModel.ResetRuntimeLayoutFields();

        var defaults = new PompoRuntimeUiLayoutSettings();
        Assert.Equal(defaults.DialogueTextBox.X.ToString(), viewModel.RuntimeLayoutDialogueTextBoxX);
        Assert.Equal(defaults.ChoiceBoxSpacing.ToString(), viewModel.RuntimeLayoutChoiceBoxSpacing);
        Assert.Contains("Reset runtime UI layout", viewModel.StatusMessage);
    }

    [Fact]
    public void ApplyWorkspaceLayoutPreset_UpdatesWorkspaceGridDefinitions()
    {
        var viewModel = new ProjectWorkspaceViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.ApplyWorkspaceLayoutPreset("graph-focus");

        Assert.Equal("graph-focus", viewModel.SelectedWorkspaceLayoutPresetId);
        Assert.Equal("Graph Focus", viewModel.SelectedWorkspaceLayoutPreset.DisplayName);
        Assert.Equal("220,5,*,5,260", viewModel.WorkspaceColumnDefinitions);
        Assert.Equal("38*,5,62*", viewModel.WorkspaceRowDefinitions);
        Assert.Contains(nameof(ProjectWorkspaceViewModel.WorkspaceColumnDefinitions), changed);
        Assert.Contains(nameof(ProjectWorkspaceViewModel.WorkspaceRowDefinitions), changed);
        Assert.Contains("Switched workspace layout", viewModel.StatusMessage);
    }

    [Fact]
    public void ApplyWorkspaceLayoutPreset_RejectsUnknownPresetWithoutChangingLayout()
    {
        var viewModel = new ProjectWorkspaceViewModel();

        viewModel.ApplyWorkspaceLayoutPreset("missing");

        Assert.Equal("balanced", viewModel.SelectedWorkspaceLayoutPresetId);
        Assert.Equal("280,5,*,5,340", viewModel.WorkspaceColumnDefinitions);
        Assert.Contains("Unknown workspace layout preset", viewModel.StatusMessage);
    }

    [Fact]
    public void WorkspacePanelVisibility_UpdatesGridDefinitionsAndCanRestorePanels()
    {
        var viewModel = new ProjectWorkspaceViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.WorkspaceProjectPanelVisible = false;
        viewModel.WorkspaceInspectorPanelVisible = false;
        viewModel.WorkspaceConsolePanelVisible = false;
        viewModel.WorkspaceScenePanelVisible = false;

        Assert.Equal("0,0,*,0,0", viewModel.WorkspaceColumnDefinitions);
        Assert.Equal("0,0,*", viewModel.WorkspaceRowDefinitions);
        Assert.False(viewModel.WorkspaceRightPanelVisible);
        Assert.False(viewModel.WorkspaceCenterSplitterVisible);
        Assert.Contains(nameof(ProjectWorkspaceViewModel.WorkspaceColumnDefinitions), changed);
        Assert.Contains(nameof(ProjectWorkspaceViewModel.WorkspaceRowDefinitions), changed);

        viewModel.ShowAllWorkspacePanels();

        Assert.True(viewModel.WorkspaceProjectPanelVisible);
        Assert.True(viewModel.WorkspaceScenePanelVisible);
        Assert.True(viewModel.WorkspaceGraphPanelVisible);
        Assert.True(viewModel.WorkspaceInspectorPanelVisible);
        Assert.True(viewModel.WorkspaceConsolePanelVisible);
        Assert.Equal("280,5,*,5,340", viewModel.WorkspaceColumnDefinitions);
        Assert.Equal("*,5,280", viewModel.WorkspaceRowDefinitions);
        Assert.Contains("Restored all workspace panels", viewModel.StatusMessage);
    }

    [Fact]
    public void WorkspacePanelVisibility_KeepsSceneOrGraphVisible()
    {
        var viewModel = new ProjectWorkspaceViewModel();

        viewModel.WorkspaceScenePanelVisible = false;
        viewModel.WorkspaceGraphPanelVisible = false;

        Assert.False(viewModel.WorkspaceScenePanelVisible);
        Assert.True(viewModel.WorkspaceGraphPanelVisible);
        Assert.Equal("0,0,*", viewModel.WorkspaceRowDefinitions);
        Assert.Contains("must keep Scene or Graph visible", viewModel.StatusMessage);
    }

    [Fact]
    public void FocusWorkspaceTarget_AppliesPresetAndPanelVisibilityTogether()
    {
        var viewModel = new ProjectWorkspaceViewModel();

        viewModel.FocusWorkspaceTarget("graph");

        Assert.Equal("graph-focus", viewModel.SelectedWorkspaceLayoutPresetId);
        Assert.False(viewModel.WorkspaceProjectPanelVisible);
        Assert.False(viewModel.WorkspaceScenePanelVisible);
        Assert.True(viewModel.WorkspaceGraphPanelVisible);
        Assert.True(viewModel.WorkspaceInspectorPanelVisible);
        Assert.True(viewModel.WorkspaceConsolePanelVisible);
        Assert.Equal("0,0,*,5,260", viewModel.WorkspaceColumnDefinitions);
        Assert.Equal("0,0,*", viewModel.WorkspaceRowDefinitions);
        Assert.Contains("Focused workspace for 'Graph'", viewModel.StatusMessage);

        viewModel.FocusWorkspaceTarget("all");

        Assert.Equal("balanced", viewModel.SelectedWorkspaceLayoutPresetId);
        Assert.True(viewModel.WorkspaceProjectPanelVisible);
        Assert.True(viewModel.WorkspaceScenePanelVisible);
        Assert.True(viewModel.WorkspaceGraphPanelVisible);
        Assert.True(viewModel.WorkspaceInspectorPanelVisible);
        Assert.True(viewModel.WorkspaceConsolePanelVisible);
    }

    [Fact]
    public void FocusWorkspaceTarget_RejectsUnknownTargetWithoutChangingLayout()
    {
        var viewModel = new ProjectWorkspaceViewModel();

        viewModel.FocusWorkspaceTarget("missing");

        Assert.Equal("balanced", viewModel.SelectedWorkspaceLayoutPresetId);
        Assert.True(viewModel.WorkspaceProjectPanelVisible);
        Assert.Contains("was not found", viewModel.StatusMessage);
    }

    [Fact]
    public async Task WorkspacePreferences_RoundTripLayoutPresetAndPanelVisibility()
    {
        var settingsPath = Path.Combine(CreateTempDirectory(), "workspace.json");
        var service = new EditorWorkspacePreferencesService(settingsPath);
        var viewModel = new ProjectWorkspaceViewModel(workspacePreferencesService: service);

        viewModel.ApplyWorkspaceLayoutPreset("review");
        viewModel.WorkspaceProjectPanelVisible = false;
        viewModel.WorkspaceScenePanelVisible = false;
        viewModel.WorkspaceInspectorPanelVisible = false;
        await viewModel.SaveWorkspacePreferencesAsync();

        var reloaded = new ProjectWorkspaceViewModel(workspacePreferencesService: service);
        await reloaded.LoadWorkspacePreferencesAsync();

        Assert.Equal("review", reloaded.SelectedWorkspaceLayoutPresetId);
        Assert.False(reloaded.WorkspaceProjectPanelVisible);
        Assert.False(reloaded.WorkspaceScenePanelVisible);
        Assert.True(reloaded.WorkspaceGraphPanelVisible);
        Assert.False(reloaded.WorkspaceInspectorPanelVisible);
        Assert.Equal("0,0,*,5,420", reloaded.WorkspaceColumnDefinitions);
        Assert.Equal("0,0,*", reloaded.WorkspaceRowDefinitions);
        Assert.Contains("Loaded workspace preferences", reloaded.StatusMessage);
    }

    [Fact]
    public async Task WorkspacePreferences_NormalizesInvalidHiddenCenterPanels()
    {
        var settingsPath = Path.Combine(CreateTempDirectory(), "workspace.json");
        var service = new EditorWorkspacePreferencesService(settingsPath);
        await service.SaveAsync(new EditorWorkspacePreferences(
            "missing",
            ProjectPanelVisible: true,
            ScenePanelVisible: false,
            GraphPanelVisible: false,
            InspectorPanelVisible: true,
            ConsolePanelVisible: true));
        var viewModel = new ProjectWorkspaceViewModel(workspacePreferencesService: service);

        await viewModel.LoadWorkspacePreferencesAsync();

        Assert.Equal("balanced", viewModel.SelectedWorkspaceLayoutPresetId);
        Assert.False(viewModel.WorkspaceScenePanelVisible);
        Assert.True(viewModel.WorkspaceGraphPanelVisible);
    }

    [Fact]
    public async Task RecentProjects_RecordCreatedProjectsAndOpenSelectedProject()
    {
        var root1 = CreateTempDirectory();
        var root2 = CreateTempDirectory();
        var settingsPath = Path.Combine(CreateTempDirectory(), "editor-recents.json");
        var viewModel = new ProjectWorkspaceViewModel(
            recentProjectsService: new EditorRecentProjectsService(settingsPath));

        await viewModel.CreateSampleProjectAsync(root1, "Recent One");
        await viewModel.CreateMinimalProjectAsync(root2, "Recent Two");

        Assert.Equal(2, viewModel.RecentProjects.Count);
        Assert.Equal(Path.GetFullPath(root2), viewModel.RecentProjects[0].ProjectRoot);
        Assert.Equal("Recent Two", viewModel.RecentProjects[0].ProjectName);

        viewModel.SelectedRecentProjectRoot = Path.GetFullPath(root1);
        await viewModel.OpenSelectedRecentProjectAsync();

        Assert.Equal(Path.GetFullPath(root1), viewModel.ProjectRoot);
        Assert.Equal("Recent One", viewModel.Summary!.ProjectName);
        Assert.Equal(Path.GetFullPath(root1), viewModel.RecentProjects[0].ProjectRoot);
        Assert.Contains("Loaded 'Recent One'", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ForgetSelectedRecentProjectAsync_RemovesRecentEntryWithoutDeletingProject()
    {
        var root = CreateTempDirectory();
        var settingsPath = Path.Combine(CreateTempDirectory(), "editor-recents.json");
        var viewModel = new ProjectWorkspaceViewModel(
            recentProjectsService: new EditorRecentProjectsService(settingsPath));
        await viewModel.CreateSampleProjectAsync(root, "Forget Recent");
        viewModel.SelectedRecentProjectRoot = Path.GetFullPath(root);

        await viewModel.ForgetSelectedRecentProjectAsync();

        Assert.Empty(viewModel.RecentProjects);
        Assert.Null(viewModel.SelectedRecentProjectRoot);
        Assert.True(File.Exists(Path.Combine(root, ProjectConstants.ProjectFileName)));
        Assert.Contains("Removed recent project", viewModel.StatusMessage);
    }

    [Fact]
    public async Task RunDoctorAsync_ReportsHealthyLoadedSampleProject()
    {
        var root = CreateTempDirectory();
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.CreateSampleProjectAsync(root, "Doctor Ready");

        Assert.Contains(
            viewModel.ProductionReadinessItems,
            item => item.Title == "Project doctor" && item.Status == "Not run" && !item.IsPassing);

        await viewModel.RunDoctorAsync();

        Assert.Empty(viewModel.DoctorDiagnostics);
        Assert.Contains("Doctor checks passed", viewModel.StatusMessage);
        Assert.Contains(
            viewModel.ProductionReadinessItems,
            item => item.Title == "Project doctor" && item.Status == "Passed" && item.IsPassing);
        Assert.Contains(
            viewModel.ProductionReadinessItems,
            item => item.Title == "Release profile" && item.Status == "Ready" && item.IsPassing);
    }

    [Fact]
    public async Task RunDoctorAsync_ReportsReleaseProfileIssues()
    {
        var root = CreateTempDirectory();
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.CreateSampleProjectAsync(root, "Doctor Issues");
        await new BuildProfileFileService().SaveAsync(
            BuildProfileFileService.GetDefaultProfilePath(root, "release"),
            new PompoBuildProfile(
                "release",
                PompoTargetPlatform.MacOS,
                "Doctor Issues",
                "0.1.0",
                RunSmokeTest: false,
                PackageRuntime: false,
                SelfContained: false));

        await viewModel.RunDoctorAsync();

        Assert.Contains(viewModel.DoctorDiagnostics, diagnostic => diagnostic.Code == "DOCTOR006");
        Assert.Contains(viewModel.DoctorDiagnostics, diagnostic => diagnostic.Code == "DOCTOR007");
        Assert.Contains(viewModel.DoctorDiagnostics, diagnostic => diagnostic.Code == "DOCTOR008");
        Assert.Contains("Doctor checks found", viewModel.StatusMessage);
        Assert.Contains(
            viewModel.ProductionReadinessItems,
            item => item.Title == "Release profile" && item.Status == "Incomplete" && !item.IsPassing);
        Assert.Contains(
            viewModel.ProductionReadinessItems,
            item => item.Title == "Project doctor" && item.Status == "Issues" && !item.IsPassing);
    }

    [Fact]
    public async Task DuplicateSelectedGraphNode_DuplicatesCurrentGraphSelection()
    {
        var root = CreateTempDirectory();
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.CreateSampleProjectAsync(root, "Duplicate Node");
        viewModel.GraphEditor!.SelectNode("opening");

        viewModel.DuplicateSelectedGraphNode();

        Assert.Contains("Duplicated graph node", viewModel.StatusMessage);
        Assert.Equal("opening_copy", viewModel.GraphEditor.SelectedNodeId);
        Assert.Contains(viewModel.GraphEditor.Nodes, node => node.NodeId == "opening_copy");
    }

    [Fact]
    public async Task SelectedGraphId_SwitchesGraphEditorAndDirtyGuardPreventsDataLoss()
    {
        var root = CreateTempDirectory();
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.CreateSampleProjectAsync(root, "Graph Switch");

        Assert.Contains("graph_cafe", viewModel.GraphOptions);
        Assert.Equal("graph_intro", viewModel.SelectedGraphId);

        viewModel.SelectedGraphId = "graph_cafe";

        Assert.Equal("graph_cafe", viewModel.SelectedGraphId);
        Assert.Equal("graph_cafe", viewModel.GraphEditor!.GraphId);
        Assert.Contains("Opened graph 'graph_cafe'", viewModel.StatusMessage);

        viewModel.AddNodeToCurrentGraph(GraphNodeKind.Narration);
        Assert.True(viewModel.GraphEditor.IsDirty);

        viewModel.SelectedGraphId = "graph_rooftop";

        Assert.Equal("graph_cafe", viewModel.SelectedGraphId);
        Assert.Equal("graph_cafe", viewModel.GraphEditor.GraphId);
        Assert.Contains("Save graph 'graph_cafe' before switching", viewModel.StatusMessage);
    }

    [Fact]
    public async Task AddGraphAsync_CreatesConnectedDefaultGraphAndSelectsIt()
    {
        var root = CreateTempDirectory();
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.CreateMinimalProjectAsync(root, "Graph Add");

        await viewModel.AddGraphAsync();

        Assert.Equal("graph_2", viewModel.SelectedGraphId);
        Assert.Equal("graph_2", viewModel.GraphEditor!.GraphId);
        Assert.False(viewModel.GraphEditor.IsDirty);
        Assert.Contains(viewModel.GraphEditor.Nodes, node => node.Kind == GraphNodeKind.Start);
        Assert.Contains(viewModel.GraphEditor.Nodes, node => node.Kind == GraphNodeKind.EndScene);
        Assert.Single(viewModel.GraphEditor.Edges);
        Assert.Contains("Added graph 'graph_2'", viewModel.StatusMessage);

        var reloaded = await new ProjectFileService().LoadAsync(root);
        Assert.Contains(reloaded.Graphs, graph => graph.GraphId == "graph_2");
    }

    [Fact]
    public async Task DeleteSelectedGraphAsync_RemovesUnreferencedGraphAndSelectsRemainingGraph()
    {
        var root = CreateTempDirectory();
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.CreateMinimalProjectAsync(root, "Graph Delete");
        await viewModel.AddGraphAsync();

        await viewModel.DeleteSelectedGraphAsync();

        Assert.Equal("graph_intro", viewModel.SelectedGraphId);
        Assert.Equal("graph_intro", viewModel.GraphEditor!.GraphId);
        Assert.DoesNotContain("graph_2", viewModel.GraphOptions);
        Assert.Contains("Deleted graph 'graph_2'", viewModel.StatusMessage);

        var reloaded = await new ProjectFileService().LoadAsync(root);
        Assert.DoesNotContain(reloaded.Graphs, graph => graph.GraphId == "graph_2");
    }

    [Fact]
    public async Task DeleteSelectedGraphAsync_RejectsReferencedGraph()
    {
        var root = CreateTempDirectory();
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.CreateSampleProjectAsync(root, "Graph Delete Guard");
        viewModel.SelectedGraphId = "graph_intro";

        await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.DeleteSelectedGraphAsync());

        Assert.Equal("graph_intro", viewModel.SelectedGraphId);
        Assert.Contains("referenced by scene 'scene_intro' start graph", viewModel.StatusMessage);
        var reloaded = await new ProjectFileService().LoadAsync(root);
        Assert.Contains(reloaded.Graphs, graph => graph.GraphId == "graph_intro");
    }

    [Fact]
    public async Task RenameSelectedGraphAsync_UpdatesSceneAndCallGraphReferences()
    {
        var root = CreateTempDirectory();
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.CreateSampleProjectAsync(root, "Graph Rename");

        viewModel.SelectedGraphId = "graph_cafe_bonus";
        viewModel.GraphIdEdit = "graph_bonus_renamed";
        await viewModel.RenameSelectedGraphAsync();

        Assert.Equal("graph_bonus_renamed", viewModel.SelectedGraphId);
        Assert.Equal("graph_bonus_renamed", viewModel.GraphEditor!.GraphId);
        Assert.Contains("Renamed graph 'graph_cafe_bonus' to 'graph_bonus_renamed'", viewModel.StatusMessage);

        var afterCallRename = await new ProjectFileService().LoadAsync(root);
        var callNode = afterCallRename.Graphs
            .Single(graph => graph.GraphId == "graph_cafe")
            .Nodes
            .Single(node => node.NodeId == "call_cafe_bonus");
        Assert.Equal("graph_bonus_renamed", callNode.Properties["graphId"]!.GetValue<string>());

        viewModel.SelectedGraphId = "graph_cafe";
        viewModel.GraphIdEdit = "graph_cafe_main";
        await viewModel.RenameSelectedGraphAsync();

        Assert.Equal("graph_cafe_main", viewModel.SelectedGraphId);
        var afterSceneRename = await new ProjectFileService().LoadAsync(root);
        Assert.Equal("graph_cafe_main", afterSceneRename.Scenes.Single(scene => scene.SceneId == "scene_cafe").StartGraphId);
        Assert.Contains(afterSceneRename.Graphs, graph => graph.GraphId == "graph_cafe_main");
        Assert.DoesNotContain(afterSceneRename.Graphs, graph => graph.GraphId == "graph_cafe");
    }

    [Fact]
    public async Task CreateSampleProjectAsync_RejectsExistingProject()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Existing");
        var viewModel = new ProjectWorkspaceViewModel();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => viewModel.CreateSampleProjectAsync(root, "New"));

        Assert.Contains("already exists", viewModel.StatusMessage);
    }

    [Fact]
    public async Task BuildCurrentAsync_UsesProfileAndReportsOutput()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        var profilePath = BuildProfileFileService.GetDefaultProfilePath(root, "editor-test");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.CreateSampleProjectAsync(root, "Editor Build");
        await new BuildProfileFileService().SaveAsync(
            profilePath,
            new PompoBuildProfile(
                "editor-test",
                PompoTargetPlatform.MacOS,
                "Editor Build",
                "0.1.0",
                PackageRuntime: false));

        await viewModel.BuildCurrentAsync(output, profilePath);

        Assert.Empty(viewModel.BuildDiagnostics);
        Assert.NotNull(viewModel.LastBuildOutputDirectory);
        Assert.Contains(viewModel.LastBuildSummaryItems, item => item.Label == "App" && item.Value == "Editor Build 0.1.0");
        Assert.Contains(viewModel.LastBuildSummaryItems, item => item.Label == "Compiled graphs" && item.Value == "4");
        Assert.Contains(viewModel.LastBuildSummaryItems, item => item.Label == "Supported locales" && item.Value == "ko, en");
        Assert.Contains("Built project", viewModel.StatusMessage);
        Assert.True(File.Exists(Path.Combine(viewModel.LastBuildOutputDirectory!, "pompo-build-manifest.json")));
        Assert.True(File.Exists(Path.Combine(viewModel.LastBuildOutputDirectory!, "Data", "graph_intro.pompo-ir.json")));
        var historyItem = Assert.Single(viewModel.BuildHistory);
        Assert.Equal("editor-test", historyItem.ProfileName);
        Assert.Equal("MacOS", historyItem.Platform);
        Assert.Equal("Success", historyItem.Status);
        Assert.Equal("Editor Build 0.1.0", historyItem.AppVersion);

        var reloadedViewModel = new ProjectWorkspaceViewModel();
        await reloadedViewModel.LoadAsync(root);
        Assert.Contains(reloadedViewModel.BuildHistory, item => item.ProfileName == "editor-test" && item.Status == "Success");

        await viewModel.PackageLastBuildAsync(releaseOutput);

        Assert.NotNull(viewModel.LastReleaseArchivePath);
        Assert.NotNull(viewModel.LastReleaseManifestPath);
        Assert.True(File.Exists(viewModel.LastReleaseArchivePath));
        Assert.True(File.Exists(viewModel.LastReleaseManifestPath));
        Assert.Contains(viewModel.ReleaseDiagnostics, diagnostic => diagnostic.Code == "REL026");
        Assert.Contains("Release verification failed", viewModel.StatusMessage);
    }

    [Fact]
    public async Task BuildSelectedProfileAsync_UsesEditorProfileAndPlatformSelection()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        var profilePath = BuildProfileFileService.GetDefaultProfilePath(root, "editor-cross");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.CreateSampleProjectAsync(root, "Editor Cross Build");
        await new BuildProfileFileService().SaveAsync(
            profilePath,
            new PompoBuildProfile(
                "editor-cross",
                PompoTargetPlatform.MacOS,
                "Editor Cross Build",
                "0.1.0",
                PackageRuntime: false));

        Assert.Contains("editor-cross", viewModel.BuildProfileOptions);
        viewModel.SelectedBuildProfileName = "editor-cross";
        Assert.Equal(profilePath, viewModel.SelectedBuildProfilePath);
        Assert.Contains("Editor Cross Build 0.1.0", viewModel.SelectedBuildProfileSummary);
        Assert.Contains("file platform MacOS", viewModel.SelectedBuildProfileSummary);
        Assert.Contains("data only", viewModel.SelectedBuildProfileSummary);
        viewModel.SelectedBuildPlatform = PompoTargetPlatform.Linux;
        await viewModel.BuildSelectedProfileAsync(output);

        Assert.Empty(viewModel.BuildDiagnostics);
        Assert.NotNull(viewModel.LastBuildOutputDirectory);
        Assert.EndsWith(Path.Combine("Linux", "editor-cross"), viewModel.LastBuildOutputDirectory);
        var manifestPath = Path.Combine(viewModel.LastBuildOutputDirectory!, "pompo-build-manifest.json");
        await using var stream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync<BuildArtifactManifest>(
            stream,
            ProjectFileService.CreateJsonOptions());
        Assert.Equal(PompoTargetPlatform.Linux, manifest!.Platform);
    }

    [Fact]
    public async Task SaveBuildProfileAsync_CreatesSelectableProfileFromEditorFields()
    {
        var root = CreateTempDirectory();
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.CreateSampleProjectAsync(root, "Profile Editor");

        viewModel.BuildProfileNameEdit = "steam-demo";
        viewModel.BuildProfileAppName = "Pompo Demo";
        viewModel.BuildProfileVersion = "1.2.3";
        viewModel.SelectedBuildPlatform = PompoTargetPlatform.Linux;
        viewModel.BuildProfilePackageRuntime = true;
        viewModel.BuildProfileRunSmokeTest = true;
        viewModel.BuildProfileSelfContained = true;

        await viewModel.SaveBuildProfileAsync();

        Assert.Equal("steam-demo", viewModel.SelectedBuildProfileName);
        Assert.Contains("steam-demo", viewModel.BuildProfileOptions);
        Assert.Contains("Saved build profile 'steam-demo'", viewModel.StatusMessage);
        Assert.Contains("Pompo Demo 1.2.3", viewModel.SelectedBuildProfileSummary);
        Assert.Contains("runtime packaged", viewModel.SelectedBuildProfileSummary);
        Assert.Contains("smoke on", viewModel.SelectedBuildProfileSummary);
        Assert.Contains("self-contained", viewModel.SelectedBuildProfileSummary);

        var profile = await new BuildProfileFileService()
            .LoadAsync(BuildProfileFileService.GetDefaultProfilePath(root, "steam-demo"));
        Assert.Equal(PompoTargetPlatform.Linux, profile.Platform);
        Assert.Equal("Pompo Demo", profile.AppName);
        Assert.Equal("1.2.3", profile.Version);
        Assert.True(profile.PackageRuntime);
        Assert.True(profile.RunSmokeTest);
        Assert.True(profile.SelfContained);
    }

    [Fact]
    public async Task DeleteSelectedBuildProfileAsync_RemovesProfileAndSelectsRemainingProfile()
    {
        var root = CreateTempDirectory();
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.CreateSampleProjectAsync(root, "Profile Delete");
        viewModel.BuildProfileNameEdit = "temporary";
        viewModel.BuildProfileAppName = "Temporary";
        await viewModel.SaveBuildProfileAsync();

        await viewModel.DeleteSelectedBuildProfileAsync();

        Assert.DoesNotContain("temporary", viewModel.BuildProfileOptions);
        Assert.NotEqual("temporary", viewModel.SelectedBuildProfileName);
        Assert.Contains("Deleted build profile 'temporary'", viewModel.StatusMessage);
        Assert.False(File.Exists(BuildProfileFileService.GetDefaultProfilePath(root, "temporary")));
    }

    [Fact]
    public async Task DeleteSelectedBuildProfileAsync_RejectsLastBuildProfile()
    {
        var root = CreateTempDirectory();
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.CreateSampleProjectAsync(root, "Profile Delete Guard");
        viewModel.SelectedBuildProfileName = "release";
        await viewModel.DeleteSelectedBuildProfileAsync();
        viewModel.SelectedBuildProfileName = "debug";

        await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.DeleteSelectedBuildProfileAsync());

        Assert.Contains("must keep at least one build profile", viewModel.StatusMessage);
        Assert.True(File.Exists(BuildProfileFileService.GetDefaultProfilePath(root, "debug")));
        Assert.Single(viewModel.BuildProfileOptions);
    }

    [Fact]
    public async Task BuildAndPackageVerifiedReleaseAsync_ForcesReleaseCandidateRuntimePackage()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        var profilePath = BuildProfileFileService.GetDefaultProfilePath(root, "release");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.CreateSampleProjectAsync(root, "Editor Release");
        await new BuildProfileFileService().SaveAsync(
            profilePath,
            new PompoBuildProfile(
                "release",
                CurrentPlatform(),
                "Editor Release",
                "0.1.0",
                RunSmokeTest: false,
                PackageRuntime: false,
                RuntimeProjectPath: Path.Combine(root, "missing-runtime.csproj")));

        await viewModel.BuildAndPackageVerifiedReleaseAsync(output, releaseOutput);

        Assert.Contains(viewModel.BuildDiagnostics, diagnostic => diagnostic.Code == "BUILD003");
        Assert.Empty(viewModel.ReleaseDiagnostics);
        Assert.NotNull(viewModel.LastBuildOutputDirectory);
        Assert.EndsWith(Path.Combine(CurrentPlatform().ToString(), "release"), viewModel.LastBuildOutputDirectory);
        Assert.Empty(viewModel.LastBuildSummaryItems);
        Assert.Null(viewModel.LastReleaseArchivePath);
        Assert.Null(viewModel.LastReleaseManifestPath);
        Assert.Contains("Build failed", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ImportAssetAsync_CopiesAssetSavesProjectAndRefreshesResources()
    {
        var root = CreateTempDirectory();
        var source = Path.Combine(CreateTempDirectory(), "opening.png");
        await File.WriteAllTextAsync(source, "image-data");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.CreateSampleProjectAsync(root, "Editor Import");

        await viewModel.ImportAssetAsync(source);

        Assert.Contains("Imported asset", viewModel.StatusMessage);
        var resource = Assert.Single(viewModel.ResourceBrowser.Resources, item => item.AssetId == "opening");
        Assert.Equal("opening", resource.AssetId);
        Assert.Equal(PompoAssetType.Image, resource.Type);
        Assert.True(File.Exists(Path.Combine(root, resource.SourcePath)));
        Assert.Equal("opening", viewModel.ResourceBrowser.SelectedResourceId);

        var reloaded = await new ProjectFileService().LoadAsync(root);
        Assert.Contains(reloaded.Assets.Assets, asset => asset.AssetId == "opening");
    }

    [Fact]
    public async Task DeleteSelectedAssetAsync_RemovesUnusedAssetAndFile()
    {
        var root = CreateTempDirectory();
        var source = Path.Combine(CreateTempDirectory(), "opening.png");
        await File.WriteAllTextAsync(source, "image-data");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.CreateSampleProjectAsync(root, "Editor Asset Delete");
        await viewModel.ImportAssetAsync(source);
        var importedPath = Path.Combine(root, "Assets", "Images", "opening.png");

        await viewModel.DeleteSelectedAssetAsync();

        Assert.Contains("Deleted asset 'opening'", viewModel.StatusMessage);
        Assert.DoesNotContain(viewModel.ResourceBrowser.Resources, resource => resource.AssetId == "opening");
        Assert.False(File.Exists(importedPath));
        var reloaded = await new ProjectFileService().LoadAsync(root);
        Assert.DoesNotContain(reloaded.Assets.Assets, asset => asset.AssetId == "opening");
    }

    [Fact]
    public async Task DeleteSelectedAssetAsync_RejectsReferencedAsset()
    {
        var root = CreateTempDirectory();
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.CreateSampleProjectAsync(root, "Editor Asset Guard");
        viewModel.ResourceBrowser.SelectedResourceId = "bg-intro";

        await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.DeleteSelectedAssetAsync());

        Assert.Contains("referenced by", viewModel.StatusMessage);
        var reloaded = await new ProjectFileService().LoadAsync(root);
        Assert.Contains(reloaded.Assets.Assets, asset => asset.AssetId == "bg-intro");
    }

    [Fact]
    public async Task LoadAsync_ListsAndDeletesRuntimeSaveSlots()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateSampleVisualNovelAsync(root, "Saves");
        await new RuntimeSaveStore().SaveAsync(
            Path.Combine(root, "Saves"),
            "quick",
            "Quick Save",
            CreateSaveData("graph_intro", "save_after_choice"));
        var viewModel = new ProjectWorkspaceViewModel();

        await viewModel.LoadAsync(root);

        var slot = Assert.Single(viewModel.SaveSlots);
        Assert.Equal("quick", slot.SlotId);
        Assert.Equal("Quick Save", slot.DisplayName);
        Assert.Equal("graph_intro:save_after_choice", slot.Location);
        Assert.Equal("quick", viewModel.SelectedSaveSlotId);

        await viewModel.DeleteSelectedSaveSlotAsync();

        Assert.Empty(viewModel.SaveSlots);
        Assert.Null(viewModel.SelectedSaveSlotId);
        Assert.Contains("Deleted save slot 'quick'", viewModel.StatusMessage);
        Assert.False(File.Exists(RuntimeSaveStore.GetSlotPath(Path.Combine(root, "Saves"), "quick")));
    }

    [Fact]
    public async Task RefreshSaveSlotsAsync_ReportsMissingProject()
    {
        var viewModel = new ProjectWorkspaceViewModel();

        await viewModel.RefreshSaveSlotsAsync();

        Assert.Contains("Open a project", viewModel.StatusMessage);
    }

    [Fact]
    public async Task LoadAsync_OpensFirstProjectGraphForEditing()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateSampleVisualNovelAsync(root, "Graph Workspace");
        var viewModel = new ProjectWorkspaceViewModel();

        await viewModel.LoadAsync(root);

        Assert.NotNull(viewModel.GraphEditor);
        Assert.Equal("graph_intro", viewModel.GraphEditor.GraphId);
        Assert.NotEmpty(viewModel.GraphEditor.Nodes);
    }

    [Fact]
    public async Task LoadAsync_OpensFirstSceneForAuthoring()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateSampleVisualNovelAsync(root, "Scene Workspace");
        var viewModel = new ProjectWorkspaceViewModel();

        await viewModel.LoadAsync(root);

        Assert.Equal("scene_intro", viewModel.SelectedSceneId);
        Assert.Equal(3, viewModel.Scenes.Count);
        Assert.Equal("Intro", viewModel.SceneDisplayName);
        Assert.Equal("graph_intro", viewModel.SceneStartGraphId);
        Assert.Equal("bg-intro", viewModel.SceneBackgroundAssetId);
        Assert.Contains("bg-cafe", viewModel.SceneBackgroundAssetOptions);
        Assert.Contains("graph_rooftop", viewModel.SceneStartGraphOptions);
        Assert.Single(viewModel.SceneLayerItems);

        viewModel.SelectedSceneId = "scene_cafe";

        Assert.Equal("mina", viewModel.SelectedScenePlacementId);
        Assert.Equal("mina", viewModel.ScenePlacementCharacterId);
        Assert.Equal("smile", viewModel.ScenePlacementExpressionId);
        Assert.Equal(RuntimeLayer.Character, viewModel.ScenePlacementLayer);
        Assert.Contains("surprised", viewModel.ScenePlacementExpressionOptions);
        Assert.Contains(RuntimeLayer.CharacterFront, viewModel.ScenePlacementLayerOptions);
    }

    [Fact]
    public async Task SaveSelectedSceneAsync_PersistsEditedSceneFields()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateSampleVisualNovelAsync(root, "Scene Save");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);

        viewModel.SelectedSceneId = "scene_cafe";
        viewModel.SceneDisplayName = "Cafe Rewrite";
        viewModel.SceneStartGraphId = "graph_rooftop";
        viewModel.SceneBackgroundAssetId = "bg-rooftop";
        await viewModel.SaveSelectedSceneAsync();

        Assert.Equal("scene_cafe", viewModel.SelectedSceneId);
        Assert.Contains("Saved scene 'scene_cafe'", viewModel.StatusMessage);
        var reloaded = await new ProjectFileService().LoadAsync(root);
        var scene = reloaded.Scenes.Single(scene => scene.SceneId == "scene_cafe");
        Assert.Equal("Cafe Rewrite", scene.DisplayName);
        Assert.Equal("graph_rooftop", scene.StartGraphId);
        Assert.Equal("bg-rooftop", scene.Layers.Single(layer => layer.Layer == RuntimeLayer.Background).Asset!.AssetId);
    }

    [Fact]
    public async Task AddAndDeleteSceneAsync_PersistsSceneListAndKeepsSelection()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateSampleVisualNovelAsync(root, "Scene Add Delete");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);

        await viewModel.AddSceneAsync();

        Assert.Equal("scene_4", viewModel.SelectedSceneId);
        Assert.Equal("New Scene", viewModel.SceneDisplayName);
        Assert.Equal("graph_intro", viewModel.SceneStartGraphId);
        Assert.Contains("Added scene 'scene_4'", viewModel.StatusMessage);
        var afterAdd = await new ProjectFileService().LoadAsync(root);
        Assert.Contains(afterAdd.Scenes, scene => scene.SceneId == "scene_4");

        await viewModel.DeleteSelectedSceneAsync();

        Assert.Equal("scene_intro", viewModel.SelectedSceneId);
        Assert.Contains("Deleted scene 'scene_4'", viewModel.StatusMessage);
        var afterDelete = await new ProjectFileService().LoadAsync(root);
        Assert.DoesNotContain(afterDelete.Scenes, scene => scene.SceneId == "scene_4");
    }

    [Fact]
    public async Task DeleteSelectedSceneAsync_KeepsLastScene()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Scene Keep Last");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);

        await viewModel.DeleteSelectedSceneAsync();

        Assert.Contains("at least one scene", viewModel.StatusMessage);
        var reloaded = await new ProjectFileService().LoadAsync(root);
        Assert.Single(reloaded.Scenes);
    }

    [Fact]
    public async Task AddSaveAndDeleteCharacterAsync_PersistsCharacterList()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateSampleVisualNovelAsync(root, "Character Add Delete");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);

        await viewModel.AddCharacterAsync();

        Assert.Equal("character_3", viewModel.SelectedCharacterId);
        Assert.Equal("New Character", viewModel.CharacterDisplayName);
        Assert.Contains("Added character 'character_3'", viewModel.StatusMessage);

        viewModel.CharacterDisplayName = "Riley";
        await viewModel.SaveSelectedCharacterAsync();

        Assert.Contains("Saved character 'character_3'", viewModel.StatusMessage);
        var afterSave = await new ProjectFileService().LoadAsync(root);
        Assert.Contains(afterSave.Characters, character =>
            character.CharacterId == "character_3" &&
            character.DisplayName == "Riley");

        await viewModel.DeleteSelectedCharacterAsync();

        Assert.Contains("Deleted character 'character_3'", viewModel.StatusMessage);
        var afterDelete = await new ProjectFileService().LoadAsync(root);
        Assert.DoesNotContain(afterDelete.Characters, character => character.CharacterId == "character_3");
        Assert.DoesNotContain("character_3", viewModel.SceneCharacterOptions);
    }

    [Fact]
    public async Task DeleteSelectedCharacterAsync_RejectsSceneReferencedCharacter()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateSampleVisualNovelAsync(root, "Character Ref Guard");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);
        viewModel.SelectedCharacterId = "mina";

        await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.DeleteSelectedCharacterAsync());

        Assert.Contains("scene placement", viewModel.StatusMessage);
        var reloaded = await new ProjectFileService().LoadAsync(root);
        Assert.Contains(reloaded.Characters, character => character.CharacterId == "mina");
    }

    [Fact]
    public async Task AddSaveAndDeleteCharacterExpressionAsync_PersistsExpressionList()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateSampleVisualNovelAsync(root, "Expression Add Delete");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);
        viewModel.SelectedCharacterId = "hero";
        var firstImageAssetId = viewModel.CharacterExpressionSpriteAssetOptions.First();

        await viewModel.AddCharacterExpressionAsync();

        Assert.Equal("expression_1", viewModel.SelectedCharacterExpressionId);
        Assert.Equal(firstImageAssetId, viewModel.CharacterExpressionSpriteAssetId);
        Assert.Contains("Added expression 'expression_1'", viewModel.StatusMessage);

        viewModel.CharacterExpressionSpriteAssetId = "mina-smile";
        viewModel.CharacterExpressionDescription = "Happy";
        await viewModel.SaveSelectedCharacterExpressionAsync();

        var afterSave = await new ProjectFileService().LoadAsync(root);
        var expression = afterSave.Characters.Single(character => character.CharacterId == "hero").Expressions.Single();
        Assert.Equal("mina-smile", expression.Sprite.AssetId);
        Assert.Equal("Happy", expression.Description);

        viewModel.CharacterDefaultExpression = string.Empty;
        await viewModel.SaveSelectedCharacterAsync();
        await viewModel.DeleteSelectedCharacterExpressionAsync();

        Assert.Contains("Deleted expression 'expression_1'", viewModel.StatusMessage);
        var afterDelete = await new ProjectFileService().LoadAsync(root);
        Assert.Empty(afterDelete.Characters.Single(character => character.CharacterId == "hero").Expressions);
    }

    [Fact]
    public async Task DeleteSelectedCharacterExpressionAsync_RejectsDefaultOrReferencedExpression()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateSampleVisualNovelAsync(root, "Expression Ref Guard");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);
        viewModel.SelectedCharacterId = "mina";
        viewModel.SelectedCharacterExpressionId = "smile";

        await viewModel.DeleteSelectedCharacterExpressionAsync();

        Assert.Contains("default expression", viewModel.StatusMessage);

        viewModel.CharacterDefaultExpression = "surprised";
        await viewModel.SaveSelectedCharacterAsync();
        viewModel.SelectedCharacterExpressionId = "smile";
        await viewModel.DeleteSelectedCharacterExpressionAsync();

        Assert.Contains("referenced by a scene or graph", viewModel.StatusMessage);
        var reloaded = await new ProjectFileService().LoadAsync(root);
        Assert.Contains(
            reloaded.Characters.Single(character => character.CharacterId == "mina").Expressions,
            expression => expression.ExpressionId == "smile");
    }

    [Fact]
    public async Task SaveSelectedSceneAsync_PersistsEditedCharacterPlacement()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateSampleVisualNovelAsync(root, "Scene Placement Save");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);

        viewModel.SelectedSceneId = "scene_cafe";
        viewModel.SelectedScenePlacementId = "mina";
        viewModel.ScenePlacementExpressionId = "surprised";
        viewModel.ScenePlacementLayer = RuntimeLayer.CharacterFront;
        viewModel.ScenePlacementX = 0.3f;
        viewModel.ScenePlacementY = 0.95f;
        await viewModel.SaveSelectedSceneAsync();

        var reloaded = await new ProjectFileService().LoadAsync(root);
        var placement = reloaded.Scenes.Single(scene => scene.SceneId == "scene_cafe").Characters.Single();
        Assert.Equal("mina", placement.CharacterId);
        Assert.Equal("surprised", placement.InitialExpressionId);
        Assert.Equal(RuntimeLayer.CharacterFront, placement.Layer);
        Assert.Equal(0.3f, placement.X);
        Assert.Equal(0.95f, placement.Y);
    }

    [Fact]
    public async Task AddAndDeleteSceneCharacterPlacementAsync_PersistsScenePlacementList()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateSampleVisualNovelAsync(root, "Scene Placement Add Delete");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);
        viewModel.SelectedSceneId = "scene_intro";

        await viewModel.AddSceneCharacterPlacementAsync();

        Assert.Equal("hero", viewModel.SelectedScenePlacementId);
        Assert.Equal("hero", viewModel.ScenePlacementCharacterId);
        Assert.Contains("Added character placement 'hero'", viewModel.StatusMessage);
        var afterAdd = await new ProjectFileService().LoadAsync(root);
        Assert.Contains(afterAdd.Scenes.Single(scene => scene.SceneId == "scene_intro").Characters, placement => placement.PlacementId == "hero");

        await viewModel.DeleteSelectedSceneCharacterPlacementAsync();

        Assert.Contains("Deleted character placement 'hero'", viewModel.StatusMessage);
        var afterDelete = await new ProjectFileService().LoadAsync(root);
        Assert.DoesNotContain(afterDelete.Scenes.Single(scene => scene.SceneId == "scene_intro").Characters, placement => placement.PlacementId == "hero");
    }

    [Fact]
    public async Task SaveCurrentGraphAsync_PersistsEditedGraph()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Graph Save");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);

        viewModel.AddNodeToCurrentGraph(GraphNodeKind.Dialogue);
        await viewModel.SaveCurrentGraphAsync();

        Assert.False(viewModel.GraphEditor!.IsDirty);
        Assert.Contains("Saved graph", viewModel.StatusMessage);
        var reloaded = await new ProjectFileService().LoadAsync(root);
        Assert.Contains(reloaded.Graphs.Single(graph => graph.GraphId == "graph_intro").Nodes, node => node.Kind == GraphNodeKind.Dialogue);
    }

    [Fact]
    public async Task AddSelectedNodeKindToCurrentGraph_AddsAnyCatalogNodeKind()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Node Palette");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);

        Assert.Contains(GraphNodeKind.UnlockCg, viewModel.AddableNodeKinds);
        viewModel.SelectedNodeKindToAdd = GraphNodeKind.UnlockCg;
        viewModel.AddSelectedNodeKindToCurrentGraph();

        Assert.Equal(GraphNodeKind.UnlockCg, viewModel.GraphEditor!.SelectedNodeKind);
        Assert.Contains(viewModel.GraphEditor.Nodes, node => node.Kind == GraphNodeKind.UnlockCg);
        Assert.Contains("Added UnlockCg node", viewModel.StatusMessage);
    }

    [Fact]
    public async Task AddSelectedCustomNodeToCurrentGraph_AddsScriptNodeWithMetadataDefaults()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Custom Node Palette");
        await File.WriteAllTextAsync(
            Path.Combine(root, "Scripts", "ReputationGateNode.cs"),
            """
            using System.Threading;
            using System.Threading.Tasks;
            using Pompo.Scripting;

            public sealed class ReputationGateNode : PompoConditionNode
            {
                [PompoNodeInput("Minimum reputation", DefaultValue = 5)]
                public int Minimum { get; init; }

                public override ValueTask<bool> EvaluateAsync(PompoRuntimeContext context, CancellationToken cancellationToken)
                {
                    return ValueTask.FromResult(true);
                }
            }
            """);
        var viewModel = new ProjectWorkspaceViewModel();

        await viewModel.LoadAsync(root);
        viewModel.AddSelectedCustomNodeToCurrentGraph();

        var customNode = Assert.Single(viewModel.GraphEditor!.Graph.Nodes, node => node.Kind == GraphNodeKind.Custom);
        Assert.Single(viewModel.CustomNodePaletteItems);
        Assert.Contains("Loaded 1 custom script node", viewModel.CustomNodePaletteStatus);
        Assert.Equal("ReputationGateNode", viewModel.SelectedCustomNodeToAdd!.DisplayName);
        Assert.Equal("ReputationGateNode", customNode.Properties["nodeType"]!.GetValue<string>());
        Assert.Equal(5, customNode.Properties["minimum"]!.GetValue<int>());
        Assert.Contains(customNode.Ports, port => port.PortId == "true");
        Assert.Contains(customNode.Ports, port => port.PortId == "false");
        Assert.Contains("Added custom node 'ReputationGateNode'", viewModel.StatusMessage);
    }

    [Fact]
    public async Task RunCurrentGraphPreview_CompilesAndExecutesLoadedGraph()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateSampleVisualNovelAsync(root, "Preview");
        var viewModel = new ProjectWorkspaceViewModel(previewRunner: new InProcessGraphPreviewRunner());
        await viewModel.LoadAsync(root);

        await viewModel.RunCurrentGraphPreviewAsync();

        Assert.True(viewModel.Preview.Success);
        Assert.Contains("Preview completed", viewModel.Preview.Summary);
        Assert.Contains(viewModel.Preview.Events, item => item.Kind == "Line" && item.Text.Contains("small bell", StringComparison.Ordinal));
        Assert.Contains(viewModel.Preview.Events, item => item.Kind == "Choice" && item.Text == "Visit the cafe");
        Assert.Equal("cafe", viewModel.Preview.Variables["route"]);
        Assert.Contains("BGM: none", viewModel.Preview.AudioSummary);
        Assert.Contains("Voice: none", viewModel.Preview.AudioSummary);
        Assert.Contains("sfx-chime", viewModel.Preview.AudioSummary);
    }

    [Fact]
    public async Task RunCurrentGraphPreview_UsesSelectedProjectLocale()
    {
        var root = CreateTempDirectory();
        await new ProjectFileService().SaveAsync(
            root,
            new PompoProjectDocument
            {
                ProjectName = "Localized Preview",
                SupportedLocales = ["ko", "en"],
                StringTables =
                [
                    new StringTableDocument(
                        "dialogue",
                        [
                            new StringTableEntry(
                                "line.hello",
                                new Dictionary<string, string>
                                {
                                    ["ko"] = "안녕",
                                    ["en"] = "Hello"
                                })
                        ])
                ],
                Graphs = [CreateLocalizedPreviewGraph()]
            });
        var viewModel = new ProjectWorkspaceViewModel(previewRunner: new InProcessGraphPreviewRunner());
        await viewModel.LoadAsync(root);

        await viewModel.RunCurrentGraphPreviewAsync();

        Assert.Equal("ko", viewModel.SelectedPreviewLocale);
        Assert.True(viewModel.Preview.Success);
        Assert.Contains(viewModel.Preview.Events, item => item.Kind == "Line" && item.Text == "안녕");

        viewModel.SelectedPreviewLocale = "en";
        await viewModel.RunCurrentGraphPreviewAsync();

        Assert.Contains(viewModel.Preview.Events, item => item.Kind == "Line" && item.Text == "Hello");
    }

    [Fact]
    public async Task RunCurrentGraphPreview_UsesProjectGraphLibraryForCallGraph()
    {
        var root = CreateTempDirectory();
        await new ProjectFileService().SaveAsync(
            root,
            new PompoProjectDocument
            {
                ProjectName = "Preview Calls",
                Graphs = [CreatePreviewCallRootGraph(), CreatePreviewCallChildGraph()]
            });
        var viewModel = new ProjectWorkspaceViewModel(previewRunner: new InProcessGraphPreviewRunner());
        await viewModel.LoadAsync(root);

        await viewModel.RunCurrentGraphPreviewAsync();

        Assert.True(viewModel.Preview.Success);
        Assert.Contains(viewModel.Preview.Events, item => item.Kind == "Line" && item.Text == "Child preview");
        Assert.Contains(viewModel.Preview.Events, item => item.Kind == "Line" && item.Text == "Root after call");
    }

    [Fact]
    public async Task LoadAsync_ExposesLocalizationEntriesAndDiagnostics()
    {
        var root = CreateTempDirectory();
        await new ProjectFileService().SaveAsync(
            root,
            new PompoProjectDocument
            {
                ProjectName = "Localization",
                SupportedLocales = ["ko", "en"],
                StringTables =
                [
                    new StringTableDocument(
                        "dialogue",
                        [
                            new StringTableEntry(
                                "line.hello",
                                new Dictionary<string, string>
                                {
                                    ["ko"] = "안녕",
                                    ["jp"] = "konnichiwa"
                                })
                        ])
                ]
            });
        var viewModel = new ProjectWorkspaceViewModel(previewRunner: new InProcessGraphPreviewRunner());

        await viewModel.LoadAsync(root);

        Assert.Equal(["ko", "en"], viewModel.PreviewLocaleOptions);
        Assert.Equal("ko", viewModel.SelectedPreviewLocale);
        Assert.Contains(viewModel.LocalizationLocales, locale => locale.Locale == "ko" && locale.IsPreviewLocale);
        var entry = Assert.Single(viewModel.LocalizationEntries);
        Assert.Equal("dialogue", entry.TableId);
        Assert.Equal("line.hello", entry.Key);
        Assert.Contains("ko=안녕", entry.ValuesSummary);
        Assert.Contains("jp=konnichiwa", entry.ValuesSummary);
        Assert.True(entry.HasMissingValues);
        Assert.True(entry.HasUnsupportedValues);
        Assert.Contains(viewModel.LocalizationDiagnostics, diagnostic => diagnostic.Code == "POMPO022");
        Assert.Contains(viewModel.LocalizationDiagnostics, diagnostic => diagnostic.Code == "POMPO023");
    }

    [Fact]
    public async Task AddSupportedLocaleAsync_AddsLocaleAndSelectsItForEditing()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Locale Add");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);

        viewModel.LocaleEdit = "ja";
        await viewModel.AddSupportedLocaleAsync();

        Assert.Contains("ja", viewModel.PreviewLocaleOptions);
        Assert.Equal("ja", viewModel.SelectedPreviewLocale);
        Assert.Contains(viewModel.LocalizationLocales, locale => locale.Locale == "ja" && locale.IsPreviewLocale);
        Assert.Contains("Added locale 'ja'", viewModel.StatusMessage);
        Assert.Contains(viewModel.LocalizationDiagnostics, diagnostic => diagnostic.Code == "POMPO022");

        var reloaded = await new ProjectFileService().LoadAsync(root);
        Assert.Contains("ja", reloaded.SupportedLocales);
    }

    [Fact]
    public async Task DeleteSelectedLocaleAsync_RemovesLocaleValuesAndKeepsRemainingLocaleSelected()
    {
        var root = CreateTempDirectory();
        await new ProjectFileService().SaveAsync(
            root,
            new PompoProjectDocument
            {
                ProjectName = "Locale Delete",
                SupportedLocales = ["ko", "en", "ja"],
                StringTables =
                [
                    new StringTableDocument(
                        "dialogue",
                        [
                            new StringTableEntry(
                                "line.hello",
                                new Dictionary<string, string>
                                {
                                    ["ko"] = "안녕",
                                    ["en"] = "Hello",
                                    ["ja"] = "こんにちは"
                                })
                        ])
                ]
            });
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);
        viewModel.SelectedPreviewLocale = "ja";

        await viewModel.DeleteSelectedLocaleAsync();

        Assert.DoesNotContain("ja", viewModel.PreviewLocaleOptions);
        Assert.NotEqual("ja", viewModel.SelectedPreviewLocale);
        Assert.Contains("Deleted locale 'ja'", viewModel.StatusMessage);
        Assert.DoesNotContain(viewModel.LocalizationDiagnostics, diagnostic => diagnostic.Code == "POMPO023");

        var reloaded = await new ProjectFileService().LoadAsync(root);
        Assert.DoesNotContain("ja", reloaded.SupportedLocales);
        Assert.DoesNotContain(
            reloaded.StringTables.Single().Entries.Single().Values.Keys,
            locale => locale == "ja");
    }

    [Fact]
    public async Task FillMissingLocalizationValuesAsync_RepairsSupportedLocalesWithoutDroppingUnsupportedValues()
    {
        var root = CreateTempDirectory();
        await new ProjectFileService().SaveAsync(
            root,
            new PompoProjectDocument
            {
                ProjectName = "Repair Localization",
                SupportedLocales = ["ko", "en"],
                StringTables =
                [
                    new StringTableDocument(
                        "dialogue",
                        [
                            new StringTableEntry(
                                "line.hello",
                                new Dictionary<string, string>
                                {
                                    ["ko"] = "안녕",
                                    ["jp"] = "konnichiwa"
                                })
                        ])
                ]
            });
        var viewModel = new ProjectWorkspaceViewModel(previewRunner: new InProcessGraphPreviewRunner());
        await viewModel.LoadAsync(root);

        await viewModel.FillMissingLocalizationValuesAsync();

        var entry = Assert.Single(viewModel.LocalizationEntries);
        Assert.False(entry.HasMissingValues);
        Assert.True(entry.HasUnsupportedValues);
        Assert.Contains("en=안녕", entry.ValuesSummary);
        Assert.Contains("jp=konnichiwa", entry.ValuesSummary);
        Assert.DoesNotContain(viewModel.LocalizationDiagnostics, diagnostic => diagnostic.Code == "POMPO022");
        Assert.Contains(viewModel.LocalizationDiagnostics, diagnostic => diagnostic.Code == "POMPO023");
        Assert.Contains("Filled missing localization values", viewModel.StatusMessage);

        var reloaded = await new ProjectFileService().LoadAsync(root);
        var values = reloaded.StringTables.Single().Entries.Single().Values;
        Assert.Equal("안녕", values["en"]);
        Assert.Equal("konnichiwa", values["jp"]);
    }

    [Fact]
    public async Task SaveLocalizationEntryAsync_AddsAndUpdatesSelectedLocaleValue()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Edit Localization");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);

        viewModel.SelectedPreviewLocale = "ko";
        viewModel.LocalizationTableId = "dialogue";
        viewModel.LocalizationKey = "intro.line";
        viewModel.LocalizationValue = "처음 만나는 장면입니다.";
        await viewModel.SaveLocalizationEntryAsync();

        Assert.Contains("Saved localization 'dialogue:intro.line' for 'ko'", viewModel.StatusMessage);
        Assert.Contains(
            viewModel.LocalizationEntries,
            entry => entry.TableId == "dialogue" && entry.Key == "intro.line" && entry.HasMissingValues);

        viewModel.SelectedPreviewLocale = "en";
        viewModel.SelectLocalizationEntry("dialogue", "intro.line");
        Assert.Equal(string.Empty, viewModel.LocalizationValue);
        viewModel.LocalizationValue = "This is the first meeting scene.";
        await viewModel.SaveLocalizationEntryAsync();

        Assert.Contains(
            viewModel.LocalizationEntries,
            entry => entry.TableId == "dialogue" &&
                entry.Key == "intro.line" &&
                !entry.HasMissingValues &&
                entry.ValuesSummary.Contains("ko=처음 만나는 장면입니다.", StringComparison.Ordinal) &&
                entry.ValuesSummary.Contains("en=This is the first meeting scene.", StringComparison.Ordinal));

        var reloaded = await new ProjectFileService().LoadAsync(root);
        var savedValues = reloaded.StringTables.Single(table => table.TableId == "dialogue")
            .Entries.Single(entry => entry.Key == "intro.line")
            .Values;
        Assert.Equal("처음 만나는 장면입니다.", savedValues["ko"]);
        Assert.Equal("This is the first meeting scene.", savedValues["en"]);
    }

    [Fact]
    public async Task SaveLocalizationEntryAsync_RejectsUnsupportedLocale()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Edit Localization Guard");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);

        viewModel.SelectedPreviewLocale = "fr";
        viewModel.LocalizationTableId = "dialogue";
        viewModel.LocalizationKey = "intro.line";
        viewModel.LocalizationValue = "bonjour";

        await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.SaveLocalizationEntryAsync());

        Assert.Contains("Locale 'fr' is not supported", viewModel.StatusMessage);
        var reloaded = await new ProjectFileService().LoadAsync(root);
        Assert.DoesNotContain(reloaded.StringTables, table => table.TableId == "dialogue");
    }

    [Fact]
    public async Task DeleteLocalizationEntryAsync_RemovesUnreferencedEntry()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Delete Localization");
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);
        viewModel.SelectedPreviewLocale = "ko";
        viewModel.LocalizationTableId = "dialogue";
        viewModel.LocalizationKey = "unused.line";
        viewModel.LocalizationValue = "삭제할 문자열";
        await viewModel.SaveLocalizationEntryAsync();

        await viewModel.DeleteLocalizationEntryAsync();

        Assert.Contains("Deleted localization 'dialogue:unused.line'", viewModel.StatusMessage);
        Assert.DoesNotContain(viewModel.LocalizationEntries, entry => entry.TableId == "dialogue" && entry.Key == "unused.line");
        var reloaded = await new ProjectFileService().LoadAsync(root);
        Assert.DoesNotContain(reloaded.StringTables, table => table.TableId == "dialogue");
    }

    [Fact]
    public async Task DeleteLocalizationEntryAsync_RejectsReferencedEntry()
    {
        var root = CreateTempDirectory();
        await new ProjectFileService().SaveAsync(
            root,
            new PompoProjectDocument
            {
                ProjectName = "Delete Localization Guard",
                StringTables =
                [
                    new StringTableDocument(
                        "dialogue",
                        [
                            new StringTableEntry(
                                "line.keep",
                                new Dictionary<string, string>
                                {
                                    ["ko"] = "남길 문자열",
                                    ["en"] = "Keep this line"
                                })
                        ])
                ],
                Graphs =
                [
                    new GraphDocument(
                        ProjectConstants.CurrentSchemaVersion,
                        "graph_intro",
                        [
                            new GraphNode(
                                "line",
                                GraphNodeKind.Narration,
                                new GraphPoint(0, 0),
                                [],
                                new JsonObject
                                {
                                    ["tableId"] = "dialogue",
                                    ["textKey"] = "line.keep"
                                })
                        ],
                        [])
                ]
            });
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);
        viewModel.SelectLocalizationEntry("dialogue", "line.keep");

        await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.DeleteLocalizationEntryAsync());

        Assert.Contains("referenced by graph 'graph_intro' node 'line'", viewModel.StatusMessage);
        var reloaded = await new ProjectFileService().LoadAsync(root);
        Assert.Contains(
            reloaded.StringTables.Single(table => table.TableId == "dialogue").Entries,
            entry => entry.Key == "line.keep");
    }

    [Fact]
    public void RunCurrentGraphPreview_ReportsMissingGraphWhenNothingIsLoaded()
    {
        var viewModel = new ProjectWorkspaceViewModel();

        viewModel.RunCurrentGraphPreview();

        Assert.False(viewModel.Preview.Success);
        Assert.Contains("Open a graph", viewModel.Preview.Summary);
    }

    [Fact]
    public async Task RunCurrentGraphPreview_ReportsCompileDiagnosticsForInvalidGraph()
    {
        var root = CreateTempDirectory();
        await new ProjectFileService().SaveAsync(
            root,
            new PompoProjectDocument
            {
                ProjectName = "Invalid Preview",
                Graphs = [new GraphDocument(ProjectConstants.CurrentSchemaVersion, "broken", [], [])]
            });
        var viewModel = new ProjectWorkspaceViewModel();
        await viewModel.LoadAsync(root);

        viewModel.RunCurrentGraphPreview();

        Assert.False(viewModel.Preview.Success);
        Assert.Contains("cannot compile", viewModel.Preview.Summary);
        Assert.Contains(viewModel.Preview.Diagnostics, diagnostic => diagnostic.Code == "GRAPH001");
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
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

    private static RuntimeSaveData CreateSaveData(string graphId, string nodeId)
    {
        return new RuntimeSaveData(
            ProjectConstants.CurrentSchemaVersion,
            graphId,
            nodeId,
            [],
            new Dictionary<string, object?> { ["route"] = "cafe" },
            "bg-intro",
            [],
            new RuntimeAudioState(null, []),
            ["Visit the cafe"]);
    }

    private static GraphDocument CreateLocalizedPreviewGraph()
    {
        var start = new GraphNode(
            "start",
            GraphNodeKind.Start,
            new GraphPoint(0, 0),
            [NodeCatalog.OutExecPort()],
            []);
        var line = new GraphNode(
            "line",
            GraphNodeKind.Narration,
            new GraphPoint(200, 0),
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject
            {
                ["tableId"] = "dialogue",
                ["textKey"] = "line.hello",
                ["text"] = "Hello"
            });
        var end = new GraphNode(
            "end",
            GraphNodeKind.EndScene,
            new GraphPoint(400, 0),
            [NodeCatalog.InExecPort()],
            []);

        return new GraphDocument(
            ProjectConstants.CurrentSchemaVersion,
            "localized",
            [start, line, end],
            [
                new GraphEdge("e1", "start", "out", "line", "in"),
                new GraphEdge("e2", "line", "out", "end", "in")
            ]);
    }

    private static GraphDocument CreatePreviewCallRootGraph()
    {
        var start = new GraphNode("start", GraphNodeKind.Start, new GraphPoint(0, 0), [NodeCatalog.OutExecPort()], []);
        var call = new GraphNode(
            "call_child",
            GraphNodeKind.CallGraph,
            new GraphPoint(200, 0),
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["graphId"] = "child" });
        var line = new GraphNode(
            "line",
            GraphNodeKind.Narration,
            new GraphPoint(400, 0),
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["text"] = "Root after call" });
        var end = new GraphNode("end", GraphNodeKind.EndScene, new GraphPoint(600, 0), [NodeCatalog.InExecPort()], []);

        return new GraphDocument(
            ProjectConstants.CurrentSchemaVersion,
            "root",
            [start, call, line, end],
            [
                new GraphEdge("e1", "start", "out", "call_child", "in"),
                new GraphEdge("e2", "call_child", "out", "line", "in"),
                new GraphEdge("e3", "line", "out", "end", "in")
            ]);
    }

    private static GraphDocument CreatePreviewCallChildGraph()
    {
        var start = new GraphNode("start", GraphNodeKind.Start, new GraphPoint(0, 0), [NodeCatalog.OutExecPort()], []);
        var line = new GraphNode(
            "line",
            GraphNodeKind.Narration,
            new GraphPoint(200, 0),
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["text"] = "Child preview" });
        var ret = new GraphNode("return", GraphNodeKind.Return, new GraphPoint(400, 0), [NodeCatalog.InExecPort()], []);

        return new GraphDocument(
            ProjectConstants.CurrentSchemaVersion,
            "child",
            [start, line, ret],
            [
                new GraphEdge("e1", "start", "out", "line", "in"),
                new GraphEdge("e2", "line", "out", "return", "in")
            ]);
    }
}
