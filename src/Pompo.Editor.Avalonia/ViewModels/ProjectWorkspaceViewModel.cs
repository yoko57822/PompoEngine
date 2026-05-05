using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pompo.Build;
using Pompo.Core.Assets;
using Pompo.Core.Characters;
using Pompo.Core.Project;
using Pompo.Core.Runtime;
using Pompo.Core.Graphs;
using Pompo.Core.Scenes;
using Pompo.Editor.Avalonia.Services;
using Pompo.Scripting;
using Pompo.VisualScripting;
using Pompo.VisualScripting.Authoring;
using Pompo.VisualScripting.Runtime;

namespace Pompo.Editor.Avalonia.ViewModels;

public sealed class ProjectWorkspaceViewModel : INotifyPropertyChanged
{
    private readonly EditorProjectWorkspaceService _workspaceService;
    private readonly IGraphPreviewRunner _previewRunner;
    private readonly EditorRecentProjectsService _recentProjectsService;
    private readonly EditorWorkspacePreferencesService _workspacePreferencesService;
    private readonly BuildHistoryService _buildHistoryService;
    private ProjectWorkspaceState? _state;
    private bool _isBusy;
    private bool _doctorHasRun;
    private string _statusMessage = "No project loaded.";
    private string? _lastBuildOutputDirectory;
    private string? _lastReleaseArchivePath;
    private string? _lastReleaseManifestPath;
    private IReadOnlyList<BuildSummaryItem> _lastBuildSummaryItems = [];
    private IReadOnlyList<BuildHistoryViewItem> _buildHistory = [];
    private IReadOnlyList<EditorDiagnostic> _buildDiagnostics = [];
    private IReadOnlyList<EditorDiagnostic> _releaseDiagnostics = [];
    private IReadOnlyList<EditorDiagnostic> _doctorDiagnostics = [];
    private IReadOnlyList<RecentProjectViewItem> _recentProjects = [];
    private string? _selectedRecentProjectRoot;
    private string _selectedWorkspaceLayoutPresetId = "balanced";
    private bool _workspaceProjectPanelVisible = true;
    private bool _workspaceScenePanelVisible = true;
    private bool _workspaceGraphPanelVisible = true;
    private bool _workspaceInspectorPanelVisible = true;
    private bool _workspaceConsolePanelVisible = true;
    private IReadOnlyList<SaveSlotViewItem> _saveSlots = [];
    private string? _selectedSaveSlotId;
    private string? _selectedPreviewLocale;
    private string _localeEdit = "ko";
    private string _localizationTableId = "ui";
    private string _localizationKey = string.Empty;
    private string _localizationValue = string.Empty;
    private string? _selectedSceneId;
    private string? _selectedScenePlacementId;
    private string? _selectedCharacterId;
    private string _sceneDisplayName = string.Empty;
    private string _sceneStartGraphId = string.Empty;
    private string _sceneBackgroundAssetId = string.Empty;
    private string _characterDisplayName = string.Empty;
    private string _characterDefaultExpression = string.Empty;
    private string? _selectedCharacterExpressionId;
    private string _characterExpressionSpriteAssetId = string.Empty;
    private string _characterExpressionDescription = string.Empty;
    private string _scenePlacementCharacterId = string.Empty;
    private string _scenePlacementExpressionId = string.Empty;
    private RuntimeLayer _scenePlacementLayer = RuntimeLayer.Character;
    private float _scenePlacementX;
    private float _scenePlacementY = 1f;
    private string _selectedBuildProfileName = "debug";
    private PompoTargetPlatform _selectedBuildPlatform = CurrentPlatform();
    private bool _buildReleaseCandidate;
    private string _buildProfileNameEdit = "debug";
    private string _buildProfileAppName = string.Empty;
    private string _buildProfileVersion = "0.1.0";
    private bool _buildProfilePackageRuntime = true;
    private bool _buildProfileRunSmokeTest;
    private bool _buildProfileSelfContained;
    private string _runtimeThemeCanvasClear = new PompoRuntimeUiTheme().CanvasClear;
    private string _runtimeThemeStageFallback = new PompoRuntimeUiTheme().StageFallback;
    private string _runtimeThemeStageActiveFallback = new PompoRuntimeUiTheme().StageActiveFallback;
    private string _runtimeThemeDialogueBackground = new PompoRuntimeUiTheme().DialogueBackground;
    private string _runtimeThemeNameBoxBackground = new PompoRuntimeUiTheme().NameBoxBackground;
    private string _runtimeThemeChoiceBackground = new PompoRuntimeUiTheme().ChoiceBackground;
    private string _runtimeThemeChoiceSelectedBackground = new PompoRuntimeUiTheme().ChoiceSelectedBackground;
    private string _runtimeThemeSaveMenuBackground = new PompoRuntimeUiTheme().SaveMenuBackground;
    private string _runtimeThemeSaveSlotBackground = new PompoRuntimeUiTheme().SaveSlotBackground;
    private string _runtimeThemeSaveSlotEmptyBackground = new PompoRuntimeUiTheme().SaveSlotEmptyBackground;
    private string _runtimeThemeBacklogBackground = new PompoRuntimeUiTheme().BacklogBackground;
    private string _runtimeThemeText = new PompoRuntimeUiTheme().Text;
    private string _runtimeThemeMutedText = new PompoRuntimeUiTheme().MutedText;
    private string _runtimeThemeAccentText = new PompoRuntimeUiTheme().AccentText;
    private string _runtimeThemeHelpText = new PompoRuntimeUiTheme().HelpText;
    private string _runtimeSkinDialogueBoxAssetId = string.Empty;
    private string _runtimeSkinNameBoxAssetId = string.Empty;
    private string _runtimeSkinChoiceBoxAssetId = string.Empty;
    private string _runtimeSkinChoiceSelectedBoxAssetId = string.Empty;
    private string _runtimeSkinChoiceDisabledBoxAssetId = string.Empty;
    private string _runtimeSkinSaveMenuPanelAssetId = string.Empty;
    private string _runtimeSkinSaveSlotAssetId = string.Empty;
    private string _runtimeSkinSaveSlotSelectedAssetId = string.Empty;
    private string _runtimeSkinSaveSlotEmptyAssetId = string.Empty;
    private string _runtimeSkinBacklogPanelAssetId = string.Empty;
    private string _runtimeLayoutDialogueTextBoxX = new PompoRuntimeUiLayoutSettings().DialogueTextBox.X.ToString();
    private string _runtimeLayoutDialogueTextBoxY = new PompoRuntimeUiLayoutSettings().DialogueTextBox.Y.ToString();
    private string _runtimeLayoutDialogueTextBoxWidth = new PompoRuntimeUiLayoutSettings().DialogueTextBox.Width.ToString();
    private string _runtimeLayoutDialogueTextBoxHeight = new PompoRuntimeUiLayoutSettings().DialogueTextBox.Height.ToString();
    private string _runtimeLayoutDialogueNameBoxX = new PompoRuntimeUiLayoutSettings().DialogueNameBox.X.ToString();
    private string _runtimeLayoutDialogueNameBoxY = new PompoRuntimeUiLayoutSettings().DialogueNameBox.Y.ToString();
    private string _runtimeLayoutDialogueNameBoxWidth = new PompoRuntimeUiLayoutSettings().DialogueNameBox.Width.ToString();
    private string _runtimeLayoutDialogueNameBoxHeight = new PompoRuntimeUiLayoutSettings().DialogueNameBox.Height.ToString();
    private string _runtimeLayoutChoiceBoxWidth = new PompoRuntimeUiLayoutSettings().ChoiceBoxWidth.ToString();
    private string _runtimeLayoutChoiceBoxHeight = new PompoRuntimeUiLayoutSettings().ChoiceBoxHeight.ToString();
    private string _runtimeLayoutChoiceBoxSpacing = new PompoRuntimeUiLayoutSettings().ChoiceBoxSpacing.ToString();
    private string _runtimeLayoutSaveMenuX = new PompoRuntimeUiLayoutSettings().SaveMenuBounds.X.ToString();
    private string _runtimeLayoutSaveMenuY = new PompoRuntimeUiLayoutSettings().SaveMenuBounds.Y.ToString();
    private string _runtimeLayoutSaveMenuWidth = new PompoRuntimeUiLayoutSettings().SaveMenuBounds.Width.ToString();
    private string _runtimeLayoutSaveMenuHeight = new PompoRuntimeUiLayoutSettings().SaveMenuBounds.Height.ToString();
    private string _runtimeLayoutSaveSlotHeight = new PompoRuntimeUiLayoutSettings().SaveSlotHeight.ToString();
    private string _runtimeLayoutSaveSlotSpacing = new PompoRuntimeUiLayoutSettings().SaveSlotSpacing.ToString();
    private string _runtimeLayoutBacklogX = new PompoRuntimeUiLayoutSettings().BacklogBounds.X.ToString();
    private string _runtimeLayoutBacklogY = new PompoRuntimeUiLayoutSettings().BacklogBounds.Y.ToString();
    private string _runtimeLayoutBacklogWidth = new PompoRuntimeUiLayoutSettings().BacklogBounds.Width.ToString();
    private string _runtimeLayoutBacklogHeight = new PompoRuntimeUiLayoutSettings().BacklogBounds.Height.ToString();
    private bool _runtimeAnimationEnabled = new PompoRuntimeUiAnimationSettings().Enabled;
    private string _runtimeAnimationPanelFadeMilliseconds = new PompoRuntimeUiAnimationSettings().PanelFadeMilliseconds.ToString();
    private string _runtimeAnimationChoicePulseMilliseconds = new PompoRuntimeUiAnimationSettings().ChoicePulseMilliseconds.ToString();
    private string _runtimeAnimationChoicePulseStrength = new PompoRuntimeUiAnimationSettings().ChoicePulseStrength.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private string _runtimeAnimationTextRevealCharactersPerSecond = new PompoRuntimeUiAnimationSettings().TextRevealCharactersPerSecond.ToString();
    private string _runtimePlaybackAutoForwardDelayMilliseconds = new PompoRuntimePlaybackSettings().AutoForwardDelayMilliseconds.ToString();
    private string _runtimePlaybackSkipIntervalMilliseconds = new PompoRuntimePlaybackSettings().SkipIntervalMilliseconds.ToString();
    private string? _selectedGraphId;
    private string _graphIdEdit = string.Empty;
    private GraphNodeKind _selectedNodeKindToAdd = GraphNodeKind.Narration;
    private IReadOnlyList<CustomNodePaletteItem> _customNodePaletteItems = [];
    private CustomNodePaletteItem? _selectedCustomNodeToAdd;
    private string _customNodePaletteStatus = "No custom script nodes loaded.";
    private GraphEditorViewModel? _graphEditor;
    private GraphPreviewState _preview = new(
        false,
        "Run preview to execute the current graph IR.",
        [],
        new Dictionary<string, object?>(),
        "BGM: none; SFX: none",
        []);

    public ProjectWorkspaceViewModel(
        EditorProjectWorkspaceService? workspaceService = null,
        IGraphPreviewRunner? previewRunner = null,
        EditorRecentProjectsService? recentProjectsService = null,
        EditorWorkspacePreferencesService? workspacePreferencesService = null,
        BuildHistoryService? buildHistoryService = null)
    {
        _workspaceService = workspaceService ?? new EditorProjectWorkspaceService();
        _previewRunner = previewRunner ?? new RuntimeProcessGraphPreviewRunner();
        _recentProjectsService = recentProjectsService ?? new EditorRecentProjectsService();
        _workspacePreferencesService = workspacePreferencesService ?? new EditorWorkspacePreferencesService();
        _buildHistoryService = buildHistoryService ?? new BuildHistoryService();
        ResourceBrowser = new ResourceBrowserViewModel([]);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<WorkspaceLayoutPreset> WorkspaceLayoutPresets { get; } =
    [
        new(
            "balanced",
            "Balanced",
            "Default project, scene, graph, inspector, and console balance.",
            "280,5,*,5,340",
            "*,5,280"),
        new(
            "graph-focus",
            "Graph Focus",
            "Prioritizes visual scripting canvas and keeps surrounding panels compact.",
            "220,5,*,5,260",
            "38*,5,62*"),
        new(
            "scene-focus",
            "Scene Focus",
            "Prioritizes scene staging and preview review while retaining graph access.",
            "240,5,6*,5,280",
            "72*,5,28*"),
        new(
            "review",
            "Review",
            "Expands project and inspector context for resource, validation, and build reviews.",
            "330,5,*,5,420",
            "*,5,220")
    ];

    public IReadOnlyList<WorkspaceFocusTarget> WorkspaceFocusTargets { get; } =
    [
        new("project", "Project", "Resource review with project, scene, graph, and console context."),
        new("scene", "Scene", "Scene staging with inspector context and graph hidden."),
        new("graph", "Graph", "Visual scripting canvas with inspector and console context."),
        new("review", "Review", "Release review with project, inspector, and console context."),
        new("all", "All", "Restore every workspace panel.")
    ];

    public IReadOnlyList<RuntimeAnimationPreset> RuntimeAnimationPresets { get; } =
    [
        new(
            "instant",
            "Instant",
            "No panel fade, no selected-choice pulse, and instant text reveal for rapid review.",
            new PompoRuntimeUiAnimationSettings(
                Enabled: false,
                PanelFadeMilliseconds: 0,
                ChoicePulseMilliseconds: 0,
                ChoicePulseStrength: 0f,
                TextRevealCharactersPerSecond: 0),
            new PompoRuntimePlaybackSettings(
                AutoForwardDelayMilliseconds: 600,
                SkipIntervalMilliseconds: 30)),
        new(
            "subtle",
            "Subtle",
            "Short fades, restrained choice feedback, and readable default text reveal.",
            new PompoRuntimeUiAnimationSettings(),
            new PompoRuntimePlaybackSettings()),
        new(
            "snappy",
            "Snappy",
            "Fast panel motion and text reveal for keyboard-heavy testing and brisk VN pacing.",
            new PompoRuntimeUiAnimationSettings(
                Enabled: true,
                PanelFadeMilliseconds: 80,
                ChoicePulseMilliseconds: 560,
                ChoicePulseStrength: 0.08f,
                TextRevealCharactersPerSecond: 80),
            new PompoRuntimePlaybackSettings(
                AutoForwardDelayMilliseconds: 850,
                SkipIntervalMilliseconds: 45)),
        new(
            "cinematic",
            "Cinematic",
            "Slower fades and reveal timing for dramatic scene presentation.",
            new PompoRuntimeUiAnimationSettings(
                Enabled: true,
                PanelFadeMilliseconds: 320,
                ChoicePulseMilliseconds: 1200,
                ChoicePulseStrength: 0.16f,
                TextRevealCharactersPerSecond: 32),
            new PompoRuntimePlaybackSettings(
                AutoForwardDelayMilliseconds: 1800,
                SkipIntervalMilliseconds: 120))
    ];

    public string RuntimeAnimationPresetSummary
    {
        get
        {
            var preset = RuntimeAnimationPresets.FirstOrDefault(PresetMatchesCurrentAnimation);
            return preset is null
                ? "Custom animation values"
                : $"{preset.DisplayName}: {preset.Description}";
        }
    }

    public ProjectWorkspaceState? State
    {
        get => _state;
        private set
        {
            if (_state == value)
            {
                return;
            }

            _state = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Summary));
            OnPropertyChanged(nameof(Diagnostics));
            OnPropertyChanged(nameof(ProjectRoot));
            OnPropertyChanged(nameof(HasProject));
            OnPropertyChanged(nameof(NoProjectLoaded));
            OnPropertyChanged(nameof(PreviewLocaleOptions));
            OnPropertyChanged(nameof(LocalizationLocales));
            OnPropertyChanged(nameof(LocalizationEntries));
            OnPropertyChanged(nameof(LocalizationDiagnostics));
            OnPropertyChanged(nameof(LocaleEdit));
            OnPropertyChanged(nameof(LocalizationTableId));
            OnPropertyChanged(nameof(LocalizationKey));
            OnPropertyChanged(nameof(LocalizationValue));
            OnPropertyChanged(nameof(Scenes));
            OnPropertyChanged(nameof(SelectedScene));
            OnPropertyChanged(nameof(SceneLayerItems));
            OnPropertyChanged(nameof(SceneCharacterPlacements));
            OnPropertyChanged(nameof(SelectedScenePlacement));
            OnPropertyChanged(nameof(SceneCharacterOptions));
            OnPropertyChanged(nameof(ScenePlacementExpressionOptions));
            OnPropertyChanged(nameof(ScenePlacementLayerOptions));
            OnPropertyChanged(nameof(SceneBackgroundAssetOptions));
            OnPropertyChanged(nameof(SceneStartGraphOptions));
            OnPropertyChanged(nameof(GraphOptions));
            OnPropertyChanged(nameof(SelectedGraphId));
            OnPropertyChanged(nameof(GraphIdEdit));
            OnPropertyChanged(nameof(CustomNodePaletteItems));
            OnPropertyChanged(nameof(SelectedCustomNodeToAdd));
            OnPropertyChanged(nameof(CustomNodePaletteStatus));
            OnPropertyChanged(nameof(Characters));
            OnPropertyChanged(nameof(SelectedCharacter));
            OnPropertyChanged(nameof(CharacterExpressions));
            OnPropertyChanged(nameof(SelectedCharacterExpression));
            OnPropertyChanged(nameof(CharacterExpressionSpriteAssetOptions));
            OnPropertyChanged(nameof(CharacterDefaultExpressionOptions));
            OnPropertyChanged(nameof(BuildProfileOptions));
            OnPropertyChanged(nameof(SelectedBuildProfilePath));
            OnPropertyChanged(nameof(SelectedBuildProfileSummary));
            OnPropertyChanged(nameof(BuildProfileNameEdit));
            OnPropertyChanged(nameof(BuildProfileAppName));
            OnPropertyChanged(nameof(BuildProfileVersion));
            OnPropertyChanged(nameof(BuildProfilePackageRuntime));
            OnPropertyChanged(nameof(BuildProfileRunSmokeTest));
            OnPropertyChanged(nameof(BuildProfileSelfContained));
            RaiseRuntimeThemePropertyChanges();
            OnPropertyChanged(nameof(ProductionReadinessItems));
        }
    }

    public ProjectDashboardSummary? Summary => State?.Summary;

    public string? ProjectRoot => State?.Summary.ProjectRoot;

    public bool HasProject => State is not null;

    public bool NoProjectLoaded => State is null;

    public IReadOnlyList<EditorDiagnostic> Diagnostics => State?.Diagnostics ?? [];

    public ResourceBrowserViewModel ResourceBrowser { get; private set; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
            {
                return;
            }

            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
            {
                return;
            }

            _statusMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProductionReadinessItems));
        }
    }

    public string SelectedWorkspaceLayoutPresetId
    {
        get => _selectedWorkspaceLayoutPresetId;
        set => ApplyWorkspaceLayoutPreset(value);
    }

    public WorkspaceLayoutPreset SelectedWorkspaceLayoutPreset =>
        WorkspaceLayoutPresets.FirstOrDefault(preset =>
            string.Equals(preset.PresetId, SelectedWorkspaceLayoutPresetId, StringComparison.Ordinal)) ??
        WorkspaceLayoutPresets[0];

    public string WorkspaceColumnDefinitions
    {
        get
        {
            var columns = SelectedWorkspaceLayoutPreset.ColumnDefinitions.Split(',');
            var rightVisible = WorkspaceInspectorPanelVisible || WorkspaceConsolePanelVisible;
            return string.Join(
                ',',
                WorkspaceProjectPanelVisible ? columns[0] : "0",
                WorkspaceProjectPanelVisible ? columns[1] : "0",
                columns[2],
                rightVisible ? columns[3] : "0",
                rightVisible ? columns[4] : "0");
        }
    }

    public string WorkspaceRowDefinitions
    {
        get
        {
            if (WorkspaceScenePanelVisible && WorkspaceGraphPanelVisible)
            {
                return SelectedWorkspaceLayoutPreset.RowDefinitions;
            }

            return WorkspaceScenePanelVisible ? "*,0,0" : "0,0,*";
        }
    }

    public bool WorkspaceProjectPanelVisible
    {
        get => _workspaceProjectPanelVisible;
        set => SetWorkspacePanelVisibility(ref _workspaceProjectPanelVisible, value, nameof(WorkspaceProjectPanelVisible));
    }

    public bool WorkspaceScenePanelVisible
    {
        get => _workspaceScenePanelVisible;
        set
        {
            if (!value && !WorkspaceGraphPanelVisible)
            {
                StatusMessage = "Workspace must keep Scene or Graph visible.";
                OnPropertyChanged();
                return;
            }

            SetWorkspacePanelVisibility(ref _workspaceScenePanelVisible, value, nameof(WorkspaceScenePanelVisible));
        }
    }

    public bool WorkspaceGraphPanelVisible
    {
        get => _workspaceGraphPanelVisible;
        set
        {
            if (!value && !WorkspaceScenePanelVisible)
            {
                StatusMessage = "Workspace must keep Scene or Graph visible.";
                OnPropertyChanged();
                return;
            }

            SetWorkspacePanelVisibility(ref _workspaceGraphPanelVisible, value, nameof(WorkspaceGraphPanelVisible));
        }
    }

    public bool WorkspaceInspectorPanelVisible
    {
        get => _workspaceInspectorPanelVisible;
        set => SetWorkspacePanelVisibility(ref _workspaceInspectorPanelVisible, value, nameof(WorkspaceInspectorPanelVisible));
    }

    public bool WorkspaceConsolePanelVisible
    {
        get => _workspaceConsolePanelVisible;
        set => SetWorkspacePanelVisibility(ref _workspaceConsolePanelVisible, value, nameof(WorkspaceConsolePanelVisible));
    }

    public bool WorkspaceRightPanelVisible => WorkspaceInspectorPanelVisible || WorkspaceConsolePanelVisible;

    public bool WorkspaceCenterSplitterVisible => WorkspaceScenePanelVisible && WorkspaceGraphPanelVisible;

    public void ApplyWorkspaceLayoutPreset(string? presetId)
    {
        var preset = WorkspaceLayoutPresets.FirstOrDefault(candidate =>
            string.Equals(candidate.PresetId, presetId, StringComparison.OrdinalIgnoreCase));
        if (preset is null)
        {
            StatusMessage = $"Unknown workspace layout preset '{presetId}'.";
            return;
        }

        if (string.Equals(_selectedWorkspaceLayoutPresetId, preset.PresetId, StringComparison.Ordinal))
        {
            StatusMessage = $"Workspace layout preset is already '{preset.DisplayName}'.";
            return;
        }

        _selectedWorkspaceLayoutPresetId = preset.PresetId;
        OnPropertyChanged(nameof(SelectedWorkspaceLayoutPresetId));
        OnPropertyChanged(nameof(SelectedWorkspaceLayoutPreset));
        OnPropertyChanged(nameof(WorkspaceColumnDefinitions));
        OnPropertyChanged(nameof(WorkspaceRowDefinitions));
        StatusMessage = $"Switched workspace layout to '{preset.DisplayName}'.";
    }

    public void FocusWorkspaceTarget(string? targetId)
    {
        var target = WorkspaceFocusTargets.FirstOrDefault(candidate =>
            string.Equals(candidate.TargetId, targetId, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            StatusMessage = $"Workspace focus target '{targetId}' was not found.";
            return;
        }

        switch (target.TargetId)
        {
            case "project":
                ApplyWorkspaceFocusState(
                    "review",
                    projectVisible: true,
                    sceneVisible: true,
                    graphVisible: true,
                    inspectorVisible: false,
                    consoleVisible: true);
                break;
            case "scene":
                ApplyWorkspaceFocusState(
                    "scene-focus",
                    projectVisible: false,
                    sceneVisible: true,
                    graphVisible: false,
                    inspectorVisible: true,
                    consoleVisible: false);
                break;
            case "graph":
                ApplyWorkspaceFocusState(
                    "graph-focus",
                    projectVisible: false,
                    sceneVisible: false,
                    graphVisible: true,
                    inspectorVisible: true,
                    consoleVisible: true);
                break;
            case "review":
                ApplyWorkspaceFocusState(
                    "review",
                    projectVisible: true,
                    sceneVisible: false,
                    graphVisible: true,
                    inspectorVisible: true,
                    consoleVisible: true);
                break;
            default:
                ApplyWorkspaceFocusState(
                    "balanced",
                    projectVisible: true,
                    sceneVisible: true,
                    graphVisible: true,
                    inspectorVisible: true,
                    consoleVisible: true);
                break;
        }

        StatusMessage = $"Focused workspace for '{target.DisplayName}'.";
    }

    public void ShowAllWorkspacePanels()
    {
        ApplyWorkspaceFocusState(
            SelectedWorkspaceLayoutPresetId,
            projectVisible: true,
            sceneVisible: true,
            graphVisible: true,
            inspectorVisible: true,
            consoleVisible: true);
        StatusMessage = "Restored all workspace panels.";
    }

    public async Task LoadWorkspacePreferencesAsync(CancellationToken cancellationToken = default)
    {
        var preferences = await _workspacePreferencesService.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (preferences is null)
        {
            return;
        }

        ApplyWorkspacePreferences(preferences);
        StatusMessage = "Loaded workspace preferences.";
    }

    public async Task SaveWorkspacePreferencesAsync(CancellationToken cancellationToken = default)
    {
        await _workspacePreferencesService
            .SaveAsync(
                new EditorWorkspacePreferences(
                    SelectedWorkspaceLayoutPresetId,
                    WorkspaceProjectPanelVisible,
                    WorkspaceScenePanelVisible,
                    WorkspaceGraphPanelVisible,
                    WorkspaceInspectorPanelVisible,
                    WorkspaceConsolePanelVisible),
                cancellationToken)
            .ConfigureAwait(false);
        StatusMessage = "Saved workspace preferences.";
    }

    public string? LastBuildOutputDirectory
    {
        get => _lastBuildOutputDirectory;
        private set
        {
            if (_lastBuildOutputDirectory == value)
            {
                return;
            }

            _lastBuildOutputDirectory = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<EditorDiagnostic> BuildDiagnostics
    {
        get => _buildDiagnostics;
        private set
        {
            if (_buildDiagnostics == value)
            {
                return;
            }

            _buildDiagnostics = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<BuildSummaryItem> LastBuildSummaryItems
    {
        get => _lastBuildSummaryItems;
        private set
        {
            if (_lastBuildSummaryItems == value)
            {
                return;
            }

            _lastBuildSummaryItems = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<BuildHistoryViewItem> BuildHistory
    {
        get => _buildHistory;
        private set
        {
            if (_buildHistory == value)
            {
                return;
            }

            _buildHistory = value;
            OnPropertyChanged();
        }
    }

    public string? LastReleaseArchivePath
    {
        get => _lastReleaseArchivePath;
        private set
        {
            if (_lastReleaseArchivePath == value)
            {
                return;
            }

            _lastReleaseArchivePath = value;
            OnPropertyChanged();
        }
    }

    public string? LastReleaseManifestPath
    {
        get => _lastReleaseManifestPath;
        private set
        {
            if (_lastReleaseManifestPath == value)
            {
                return;
            }

            _lastReleaseManifestPath = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<EditorDiagnostic> ReleaseDiagnostics
    {
        get => _releaseDiagnostics;
        private set
        {
            if (_releaseDiagnostics == value)
            {
                return;
            }

            _releaseDiagnostics = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<EditorDiagnostic> DoctorDiagnostics
    {
        get => _doctorDiagnostics;
        private set
        {
            if (_doctorDiagnostics == value)
            {
                return;
            }

            _doctorDiagnostics = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProductionReadinessItems));
        }
    }

    public IReadOnlyList<RecentProjectViewItem> RecentProjects
    {
        get => _recentProjects;
        private set
        {
            if (_recentProjects == value)
            {
                return;
            }

            _recentProjects = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedRecentProject));
        }
    }

    public string? SelectedRecentProjectRoot
    {
        get => _selectedRecentProjectRoot;
        set
        {
            if (_selectedRecentProjectRoot == value)
            {
                return;
            }

            _selectedRecentProjectRoot = value;
            RefreshRecentProjectSelection();
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedRecentProject));
        }
    }

    public RecentProjectViewItem? SelectedRecentProject => SelectedRecentProjectRoot is null
        ? null
        : RecentProjects.FirstOrDefault(project =>
            string.Equals(project.ProjectRoot, SelectedRecentProjectRoot, StringComparison.Ordinal));

    public IReadOnlyList<SaveSlotViewItem> SaveSlots
    {
        get => _saveSlots;
        private set
        {
            if (_saveSlots == value)
            {
                return;
            }

            _saveSlots = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedSaveSlot));
        }
    }

    public string? SelectedSaveSlotId
    {
        get => _selectedSaveSlotId;
        set
        {
            if (_selectedSaveSlotId == value)
            {
                return;
            }

            _selectedSaveSlotId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedSaveSlot));
        }
    }

    public SaveSlotViewItem? SelectedSaveSlot => SelectedSaveSlotId is null
        ? null
        : SaveSlots.FirstOrDefault(slot => string.Equals(slot.SlotId, SelectedSaveSlotId, StringComparison.Ordinal));

    public IReadOnlyList<string> PreviewLocaleOptions => State?.Project.SupportedLocales ?? [];

    public string? SelectedPreviewLocale
    {
        get => _selectedPreviewLocale;
        set
        {
            if (_selectedPreviewLocale == value)
            {
                return;
            }

            _selectedPreviewLocale = value;
            _localeEdit = value ?? string.Empty;
            SyncLocalizationValueFromFields();
            OnPropertyChanged();
            OnPropertyChanged(nameof(LocaleEdit));
            OnPropertyChanged(nameof(LocalizationLocales));
            OnPropertyChanged(nameof(ProductionReadinessItems));
        }
    }

    public string LocaleEdit
    {
        get => _localeEdit;
        set
        {
            if (_localeEdit == value)
            {
                return;
            }

            _localeEdit = value;
            OnPropertyChanged();
        }
    }

    public string LocalizationTableId
    {
        get => _localizationTableId;
        set
        {
            if (_localizationTableId == value)
            {
                return;
            }

            _localizationTableId = value;
            SyncLocalizationValueFromFields();
            OnPropertyChanged();
        }
    }

    public string LocalizationKey
    {
        get => _localizationKey;
        set
        {
            if (_localizationKey == value)
            {
                return;
            }

            _localizationKey = value;
            SyncLocalizationValueFromFields();
            OnPropertyChanged();
        }
    }

    public string LocalizationValue
    {
        get => _localizationValue;
        set
        {
            if (_localizationValue == value)
            {
                return;
            }

            _localizationValue = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<LocalizationLocaleItem> LocalizationLocales => State?.Project.SupportedLocales
        .Select(locale => new LocalizationLocaleItem(
            locale,
            string.Equals(locale, SelectedPreviewLocale, StringComparison.Ordinal)))
        .ToArray() ?? [];

    public IReadOnlyList<LocalizationStringEntryItem> LocalizationEntries => CreateLocalizationEntries();

    public IReadOnlyList<EditorDiagnostic> LocalizationDiagnostics => Diagnostics
        .Where(diagnostic => IsLocalizationDiagnosticCode(diagnostic.Code))
        .ToArray();

    public IReadOnlyList<SceneViewItem> Scenes => State?.Project.Scenes
        .Select(scene => new SceneViewItem(
            scene.SceneId,
            scene.DisplayName,
            scene.StartGraphId,
            GetSceneBackgroundAssetId(scene),
            scene.Characters.Count,
            string.Equals(scene.SceneId, SelectedSceneId, StringComparison.Ordinal)))
        .ToArray() ?? [];

    public string? SelectedSceneId
    {
        get => _selectedSceneId;
        set
        {
            if (_selectedSceneId == value)
            {
                return;
            }

            _selectedSceneId = value;
            SyncSceneFieldsFromSelection();
            OnPropertyChanged();
        OnPropertyChanged(nameof(Scenes));
        OnPropertyChanged(nameof(SelectedScene));
        OnPropertyChanged(nameof(SceneLayerItems));
        OnPropertyChanged(nameof(SceneCharacterPlacements));
        OnPropertyChanged(nameof(SelectedScenePlacement));
        OnPropertyChanged(nameof(SceneCharacterOptions));
        OnPropertyChanged(nameof(ScenePlacementExpressionOptions));
        OnPropertyChanged(nameof(ScenePlacementLayerOptions));
        OnPropertyChanged(nameof(BuildProfileOptions));
        if (BuildProfileOptions.Count > 0 &&
            BuildProfileOptions.All(profile => !string.Equals(profile, SelectedBuildProfileName, StringComparison.Ordinal)))
        {
            SelectedBuildProfileName = BuildProfileOptions.First();
        }
        OnPropertyChanged(nameof(SelectedBuildProfilePath));
        OnPropertyChanged(nameof(SelectedBuildProfileSummary));
    }
    }

    public SceneDefinition? SelectedScene => SelectedSceneId is null || State is null
        ? null
        : State.Project.Scenes.FirstOrDefault(scene => string.Equals(scene.SceneId, SelectedSceneId, StringComparison.Ordinal));

    public string SceneDisplayName
    {
        get => _sceneDisplayName;
        set
        {
            if (_sceneDisplayName == value)
            {
                return;
            }

            _sceneDisplayName = value;
            OnPropertyChanged();
        }
    }

    public string SceneStartGraphId
    {
        get => _sceneStartGraphId;
        set
        {
            if (_sceneStartGraphId == value)
            {
                return;
            }

            _sceneStartGraphId = value;
            OnPropertyChanged();
        }
    }

    public string SceneBackgroundAssetId
    {
        get => _sceneBackgroundAssetId;
        set
        {
            if (_sceneBackgroundAssetId == value)
            {
                return;
            }

            _sceneBackgroundAssetId = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<string> SceneStartGraphOptions => State?.Project.Graphs
        .Select(graph => graph.GraphId)
        .ToArray() ?? [];

    public IReadOnlyList<string> SceneBackgroundAssetOptions => State?.Project.Assets.Assets
        .Where(asset => asset.Type == PompoAssetType.Image)
        .Select(asset => asset.AssetId)
        .Prepend(string.Empty)
        .ToArray() ?? [];

    public IReadOnlyList<SceneLayerViewItem> SceneLayerItems => SelectedScene?.Layers
        .Select(layer => new SceneLayerViewItem(
            layer.LayerId,
            layer.Layer,
            layer.Asset?.AssetId ?? string.Empty,
            layer.X,
            layer.Y,
            layer.Opacity))
        .ToArray() ?? [];

    public IReadOnlyList<SceneCharacterPlacementViewItem> SceneCharacterPlacements => SelectedScene?.Characters
        .Select(character => new SceneCharacterPlacementViewItem(
            character.PlacementId,
            character.CharacterId,
            character.Layer,
            character.X,
            character.Y,
            character.InitialExpressionId ?? string.Empty,
            string.Equals(character.PlacementId, SelectedScenePlacementId, StringComparison.Ordinal)))
        .ToArray() ?? [];

    public string? SelectedScenePlacementId
    {
        get => _selectedScenePlacementId;
        set
        {
            if (_selectedScenePlacementId == value)
            {
                return;
            }

            _selectedScenePlacementId = value;
            SyncScenePlacementFieldsFromSelection();
            OnPropertyChanged();
            OnPropertyChanged(nameof(SceneCharacterPlacements));
            OnPropertyChanged(nameof(SelectedScenePlacement));
        }
    }

    public SceneCharacterPlacement? SelectedScenePlacement => SelectedScenePlacementId is null || SelectedScene is null
        ? null
        : SelectedScene.Characters.FirstOrDefault(
            character => string.Equals(character.PlacementId, SelectedScenePlacementId, StringComparison.Ordinal));

    public IReadOnlyList<string> SceneCharacterOptions => State?.Project.Characters
        .Select(character => character.CharacterId)
        .ToArray() ?? [];

    public IReadOnlyList<string> ScenePlacementExpressionOptions => State?.Project.Characters
        .FirstOrDefault(character => string.Equals(character.CharacterId, ScenePlacementCharacterId, StringComparison.Ordinal))
        ?.Expressions.Select(expression => expression.ExpressionId)
        .Prepend(string.Empty)
        .ToArray() ?? [string.Empty];

    public IReadOnlyList<RuntimeLayer> ScenePlacementLayerOptions { get; } =
        Enum.GetValues<RuntimeLayer>()
            .Where(layer => layer is RuntimeLayer.CharacterBack or RuntimeLayer.Character or RuntimeLayer.CharacterFront)
            .ToArray();

    public IReadOnlyList<CharacterViewItem> Characters => State?.Project.Characters
        .Select(character => new CharacterViewItem(
            character.CharacterId,
            character.DisplayName,
            character.DefaultExpression ?? string.Empty,
            character.Expressions.Count,
            string.Equals(character.CharacterId, SelectedCharacterId, StringComparison.Ordinal)))
        .ToArray() ?? [];

    public string? SelectedCharacterId
    {
        get => _selectedCharacterId;
        set
        {
            if (_selectedCharacterId == value)
            {
                return;
            }

            _selectedCharacterId = value;
            SyncCharacterFieldsFromSelection();
            OnPropertyChanged();
            OnPropertyChanged(nameof(Characters));
            OnPropertyChanged(nameof(SelectedCharacter));
            OnPropertyChanged(nameof(CharacterExpressions));
            OnPropertyChanged(nameof(SelectedCharacterExpression));
            OnPropertyChanged(nameof(CharacterDefaultExpressionOptions));
        }
    }

    public CharacterDefinition? SelectedCharacter => SelectedCharacterId is null || State is null
        ? null
        : State.Project.Characters.FirstOrDefault(
            character => string.Equals(character.CharacterId, SelectedCharacterId, StringComparison.Ordinal));

    public string CharacterDisplayName
    {
        get => _characterDisplayName;
        set
        {
            if (_characterDisplayName == value)
            {
                return;
            }

            _characterDisplayName = value;
            OnPropertyChanged();
        }
    }

    public string CharacterDefaultExpression
    {
        get => _characterDefaultExpression;
        set
        {
            if (_characterDefaultExpression == value)
            {
                return;
            }

            _characterDefaultExpression = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<string> CharacterDefaultExpressionOptions => SelectedCharacter?.Expressions
        .Select(expression => expression.ExpressionId)
        .Prepend(string.Empty)
        .ToArray() ?? [string.Empty];

    public IReadOnlyList<CharacterExpressionViewItem> CharacterExpressions => SelectedCharacter?.Expressions
        .Select(expression => new CharacterExpressionViewItem(
            expression.ExpressionId,
            expression.Sprite.AssetId,
            expression.Description ?? string.Empty,
            string.Equals(expression.ExpressionId, SelectedCharacterExpressionId, StringComparison.Ordinal)))
        .ToArray() ?? [];

    public string? SelectedCharacterExpressionId
    {
        get => _selectedCharacterExpressionId;
        set
        {
            if (_selectedCharacterExpressionId == value)
            {
                return;
            }

            _selectedCharacterExpressionId = value;
            SyncCharacterExpressionFieldsFromSelection();
            OnPropertyChanged();
            OnPropertyChanged(nameof(CharacterExpressions));
            OnPropertyChanged(nameof(SelectedCharacterExpression));
        }
    }

    public CharacterExpression? SelectedCharacterExpression => SelectedCharacterExpressionId is null || SelectedCharacter is null
        ? null
        : SelectedCharacter.Expressions.FirstOrDefault(
            expression => string.Equals(expression.ExpressionId, SelectedCharacterExpressionId, StringComparison.Ordinal));

    public string CharacterExpressionSpriteAssetId
    {
        get => _characterExpressionSpriteAssetId;
        set
        {
            if (_characterExpressionSpriteAssetId == value)
            {
                return;
            }

            _characterExpressionSpriteAssetId = value;
            OnPropertyChanged();
        }
    }

    public string CharacterExpressionDescription
    {
        get => _characterExpressionDescription;
        set
        {
            if (_characterExpressionDescription == value)
            {
                return;
            }

            _characterExpressionDescription = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<string> CharacterExpressionSpriteAssetOptions => State?.Project.Assets.Assets
        .Where(asset => asset.Type == PompoAssetType.Image)
        .Select(asset => asset.AssetId)
        .ToArray() ?? [];

    public IReadOnlyList<string> BuildProfileOptions
    {
        get
        {
            if (ProjectRoot is null)
            {
                return [];
            }

            var directory = Path.Combine(ProjectRoot, "BuildProfiles");
            return Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, "*.pompo-build.json")
                    .Select(path => Path.GetFileNameWithoutExtension(path).Replace(".pompo-build", string.Empty, StringComparison.Ordinal))
                    .Order(StringComparer.Ordinal)
                    .ToArray()
                : [];
        }
    }

    public IReadOnlyList<PompoTargetPlatform> BuildPlatformOptions { get; } = Enum.GetValues<PompoTargetPlatform>();

    public string SelectedBuildProfileName
    {
        get => _selectedBuildProfileName;
        set
        {
            if (_selectedBuildProfileName == value)
            {
                return;
            }

            _selectedBuildProfileName = value;
            SyncBuildProfileFieldsFromSelection();
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedBuildProfilePath));
            OnPropertyChanged(nameof(SelectedBuildProfileSummary));
            OnPropertyChanged(nameof(ProductionReadinessItems));
        }
    }

    public string BuildProfileNameEdit
    {
        get => _buildProfileNameEdit;
        set
        {
            if (_buildProfileNameEdit == value)
            {
                return;
            }

            _buildProfileNameEdit = value;
            OnPropertyChanged();
        }
    }

    public string BuildProfileAppName
    {
        get => _buildProfileAppName;
        set
        {
            if (_buildProfileAppName == value)
            {
                return;
            }

            _buildProfileAppName = value;
            OnPropertyChanged();
        }
    }

    public string BuildProfileVersion
    {
        get => _buildProfileVersion;
        set
        {
            if (_buildProfileVersion == value)
            {
                return;
            }

            _buildProfileVersion = value;
            OnPropertyChanged();
        }
    }

    public bool BuildProfilePackageRuntime
    {
        get => _buildProfilePackageRuntime;
        set
        {
            if (_buildProfilePackageRuntime == value)
            {
                return;
            }

            _buildProfilePackageRuntime = value;
            OnPropertyChanged();
        }
    }

    public bool BuildProfileRunSmokeTest
    {
        get => _buildProfileRunSmokeTest;
        set
        {
            if (_buildProfileRunSmokeTest == value)
            {
                return;
            }

            _buildProfileRunSmokeTest = value;
            OnPropertyChanged();
        }
    }

    public bool BuildProfileSelfContained
    {
        get => _buildProfileSelfContained;
        set
        {
            if (_buildProfileSelfContained == value)
            {
                return;
            }

            _buildProfileSelfContained = value;
            OnPropertyChanged();
        }
    }

    public string RuntimeThemeCanvasClear
    {
        get => _runtimeThemeCanvasClear;
        set => SetRuntimeThemeField(ref _runtimeThemeCanvasClear, value);
    }

    public string RuntimeThemeStageFallback
    {
        get => _runtimeThemeStageFallback;
        set => SetRuntimeThemeField(ref _runtimeThemeStageFallback, value);
    }

    public string RuntimeThemeStageActiveFallback
    {
        get => _runtimeThemeStageActiveFallback;
        set => SetRuntimeThemeField(ref _runtimeThemeStageActiveFallback, value);
    }

    public string RuntimeThemeDialogueBackground
    {
        get => _runtimeThemeDialogueBackground;
        set => SetRuntimeThemeField(ref _runtimeThemeDialogueBackground, value);
    }

    public string RuntimeThemeNameBoxBackground
    {
        get => _runtimeThemeNameBoxBackground;
        set => SetRuntimeThemeField(ref _runtimeThemeNameBoxBackground, value);
    }

    public string RuntimeThemeChoiceBackground
    {
        get => _runtimeThemeChoiceBackground;
        set => SetRuntimeThemeField(ref _runtimeThemeChoiceBackground, value);
    }

    public string RuntimeThemeChoiceSelectedBackground
    {
        get => _runtimeThemeChoiceSelectedBackground;
        set => SetRuntimeThemeField(ref _runtimeThemeChoiceSelectedBackground, value);
    }

    public string RuntimeThemeSaveMenuBackground
    {
        get => _runtimeThemeSaveMenuBackground;
        set => SetRuntimeThemeField(ref _runtimeThemeSaveMenuBackground, value);
    }

    public string RuntimeThemeSaveSlotBackground
    {
        get => _runtimeThemeSaveSlotBackground;
        set => SetRuntimeThemeField(ref _runtimeThemeSaveSlotBackground, value);
    }

    public string RuntimeThemeSaveSlotEmptyBackground
    {
        get => _runtimeThemeSaveSlotEmptyBackground;
        set => SetRuntimeThemeField(ref _runtimeThemeSaveSlotEmptyBackground, value);
    }

    public string RuntimeThemeBacklogBackground
    {
        get => _runtimeThemeBacklogBackground;
        set => SetRuntimeThemeField(ref _runtimeThemeBacklogBackground, value);
    }

    public string RuntimeThemeText
    {
        get => _runtimeThemeText;
        set => SetRuntimeThemeField(ref _runtimeThemeText, value);
    }

    public string RuntimeThemeMutedText
    {
        get => _runtimeThemeMutedText;
        set => SetRuntimeThemeField(ref _runtimeThemeMutedText, value);
    }

    public string RuntimeThemeAccentText
    {
        get => _runtimeThemeAccentText;
        set => SetRuntimeThemeField(ref _runtimeThemeAccentText, value);
    }

    public string RuntimeThemeHelpText
    {
        get => _runtimeThemeHelpText;
        set => SetRuntimeThemeField(ref _runtimeThemeHelpText, value);
    }

    public string RuntimeSkinDialogueBoxAssetId
    {
        get => _runtimeSkinDialogueBoxAssetId;
        set => SetRuntimeThemeField(ref _runtimeSkinDialogueBoxAssetId, value);
    }

    public string RuntimeSkinNameBoxAssetId
    {
        get => _runtimeSkinNameBoxAssetId;
        set => SetRuntimeThemeField(ref _runtimeSkinNameBoxAssetId, value);
    }

    public string RuntimeSkinChoiceBoxAssetId
    {
        get => _runtimeSkinChoiceBoxAssetId;
        set => SetRuntimeThemeField(ref _runtimeSkinChoiceBoxAssetId, value);
    }

    public string RuntimeSkinChoiceSelectedBoxAssetId
    {
        get => _runtimeSkinChoiceSelectedBoxAssetId;
        set => SetRuntimeThemeField(ref _runtimeSkinChoiceSelectedBoxAssetId, value);
    }

    public string RuntimeSkinChoiceDisabledBoxAssetId
    {
        get => _runtimeSkinChoiceDisabledBoxAssetId;
        set => SetRuntimeThemeField(ref _runtimeSkinChoiceDisabledBoxAssetId, value);
    }

    public string RuntimeSkinSaveMenuPanelAssetId
    {
        get => _runtimeSkinSaveMenuPanelAssetId;
        set => SetRuntimeThemeField(ref _runtimeSkinSaveMenuPanelAssetId, value);
    }

    public string RuntimeSkinSaveSlotAssetId
    {
        get => _runtimeSkinSaveSlotAssetId;
        set => SetRuntimeThemeField(ref _runtimeSkinSaveSlotAssetId, value);
    }

    public string RuntimeSkinSaveSlotSelectedAssetId
    {
        get => _runtimeSkinSaveSlotSelectedAssetId;
        set => SetRuntimeThemeField(ref _runtimeSkinSaveSlotSelectedAssetId, value);
    }

    public string RuntimeSkinSaveSlotEmptyAssetId
    {
        get => _runtimeSkinSaveSlotEmptyAssetId;
        set => SetRuntimeThemeField(ref _runtimeSkinSaveSlotEmptyAssetId, value);
    }

    public string RuntimeSkinBacklogPanelAssetId
    {
        get => _runtimeSkinBacklogPanelAssetId;
        set => SetRuntimeThemeField(ref _runtimeSkinBacklogPanelAssetId, value);
    }

    public string RuntimeLayoutDialogueTextBoxX
    {
        get => _runtimeLayoutDialogueTextBoxX;
        set => SetRuntimeThemeField(ref _runtimeLayoutDialogueTextBoxX, value);
    }

    public string RuntimeLayoutDialogueTextBoxY
    {
        get => _runtimeLayoutDialogueTextBoxY;
        set => SetRuntimeThemeField(ref _runtimeLayoutDialogueTextBoxY, value);
    }

    public string RuntimeLayoutDialogueTextBoxWidth
    {
        get => _runtimeLayoutDialogueTextBoxWidth;
        set => SetRuntimeThemeField(ref _runtimeLayoutDialogueTextBoxWidth, value);
    }

    public string RuntimeLayoutDialogueTextBoxHeight
    {
        get => _runtimeLayoutDialogueTextBoxHeight;
        set => SetRuntimeThemeField(ref _runtimeLayoutDialogueTextBoxHeight, value);
    }

    public string RuntimeLayoutDialogueNameBoxX
    {
        get => _runtimeLayoutDialogueNameBoxX;
        set => SetRuntimeThemeField(ref _runtimeLayoutDialogueNameBoxX, value);
    }

    public string RuntimeLayoutDialogueNameBoxY
    {
        get => _runtimeLayoutDialogueNameBoxY;
        set => SetRuntimeThemeField(ref _runtimeLayoutDialogueNameBoxY, value);
    }

    public string RuntimeLayoutDialogueNameBoxWidth
    {
        get => _runtimeLayoutDialogueNameBoxWidth;
        set => SetRuntimeThemeField(ref _runtimeLayoutDialogueNameBoxWidth, value);
    }

    public string RuntimeLayoutDialogueNameBoxHeight
    {
        get => _runtimeLayoutDialogueNameBoxHeight;
        set => SetRuntimeThemeField(ref _runtimeLayoutDialogueNameBoxHeight, value);
    }

    public string RuntimeLayoutChoiceBoxWidth
    {
        get => _runtimeLayoutChoiceBoxWidth;
        set => SetRuntimeThemeField(ref _runtimeLayoutChoiceBoxWidth, value);
    }

    public string RuntimeLayoutChoiceBoxHeight
    {
        get => _runtimeLayoutChoiceBoxHeight;
        set => SetRuntimeThemeField(ref _runtimeLayoutChoiceBoxHeight, value);
    }

    public string RuntimeLayoutChoiceBoxSpacing
    {
        get => _runtimeLayoutChoiceBoxSpacing;
        set => SetRuntimeThemeField(ref _runtimeLayoutChoiceBoxSpacing, value);
    }

    public string RuntimeLayoutSaveMenuX
    {
        get => _runtimeLayoutSaveMenuX;
        set => SetRuntimeThemeField(ref _runtimeLayoutSaveMenuX, value);
    }

    public string RuntimeLayoutSaveMenuY
    {
        get => _runtimeLayoutSaveMenuY;
        set => SetRuntimeThemeField(ref _runtimeLayoutSaveMenuY, value);
    }

    public string RuntimeLayoutSaveMenuWidth
    {
        get => _runtimeLayoutSaveMenuWidth;
        set => SetRuntimeThemeField(ref _runtimeLayoutSaveMenuWidth, value);
    }

    public string RuntimeLayoutSaveMenuHeight
    {
        get => _runtimeLayoutSaveMenuHeight;
        set => SetRuntimeThemeField(ref _runtimeLayoutSaveMenuHeight, value);
    }

    public string RuntimeLayoutSaveSlotHeight
    {
        get => _runtimeLayoutSaveSlotHeight;
        set => SetRuntimeThemeField(ref _runtimeLayoutSaveSlotHeight, value);
    }

    public string RuntimeLayoutSaveSlotSpacing
    {
        get => _runtimeLayoutSaveSlotSpacing;
        set => SetRuntimeThemeField(ref _runtimeLayoutSaveSlotSpacing, value);
    }

    public string RuntimeLayoutBacklogX
    {
        get => _runtimeLayoutBacklogX;
        set => SetRuntimeThemeField(ref _runtimeLayoutBacklogX, value);
    }

    public string RuntimeLayoutBacklogY
    {
        get => _runtimeLayoutBacklogY;
        set => SetRuntimeThemeField(ref _runtimeLayoutBacklogY, value);
    }

    public string RuntimeLayoutBacklogWidth
    {
        get => _runtimeLayoutBacklogWidth;
        set => SetRuntimeThemeField(ref _runtimeLayoutBacklogWidth, value);
    }

    public string RuntimeLayoutBacklogHeight
    {
        get => _runtimeLayoutBacklogHeight;
        set => SetRuntimeThemeField(ref _runtimeLayoutBacklogHeight, value);
    }

    public bool RuntimeAnimationEnabled
    {
        get => _runtimeAnimationEnabled;
        set
        {
            if (_runtimeAnimationEnabled == value)
            {
                return;
            }

            _runtimeAnimationEnabled = value;
            OnPropertyChanged();
        }
    }

    public string RuntimeAnimationPanelFadeMilliseconds
    {
        get => _runtimeAnimationPanelFadeMilliseconds;
        set => SetRuntimeThemeField(ref _runtimeAnimationPanelFadeMilliseconds, value);
    }

    public string RuntimeAnimationChoicePulseMilliseconds
    {
        get => _runtimeAnimationChoicePulseMilliseconds;
        set => SetRuntimeThemeField(ref _runtimeAnimationChoicePulseMilliseconds, value);
    }

    public string RuntimeAnimationChoicePulseStrength
    {
        get => _runtimeAnimationChoicePulseStrength;
        set => SetRuntimeThemeField(ref _runtimeAnimationChoicePulseStrength, value);
    }

    public string RuntimeAnimationTextRevealCharactersPerSecond
    {
        get => _runtimeAnimationTextRevealCharactersPerSecond;
        set => SetRuntimeThemeField(ref _runtimeAnimationTextRevealCharactersPerSecond, value);
    }

    public string RuntimePlaybackAutoForwardDelayMilliseconds
    {
        get => _runtimePlaybackAutoForwardDelayMilliseconds;
        set => SetRuntimeThemeField(ref _runtimePlaybackAutoForwardDelayMilliseconds, value);
    }

    public string RuntimePlaybackSkipIntervalMilliseconds
    {
        get => _runtimePlaybackSkipIntervalMilliseconds;
        set => SetRuntimeThemeField(ref _runtimePlaybackSkipIntervalMilliseconds, value);
    }

    public string? SelectedBuildProfilePath => ResolveSelectedBuildProfilePath();

    public string SelectedBuildProfileSummary
    {
        get
        {
            var path = SelectedBuildProfilePath;
            if (path is null)
            {
                return "No project loaded.";
            }

            if (!File.Exists(path))
            {
                return $"Profile file not found: {Path.GetFileName(path)}";
            }

            try
            {
                using var stream = File.OpenRead(path);
                var profile = JsonSerializer.Deserialize<PompoBuildProfile>(
                    stream,
                    ProjectFileService.CreateJsonOptions());
                if (profile is null)
                {
                    return "Profile file is empty.";
                }

                var runtime = profile.PackageRuntime ? "runtime packaged" : "data only";
                var smoke = profile.RunSmokeTest ? "smoke on" : "smoke off";
                var selfContained = profile.SelfContained ? "self-contained" : "framework-dependent";
                return $"{profile.AppName} {profile.Version} | file platform {profile.Platform} | {runtime}, {smoke}, {selfContained}";
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
            {
                return $"Profile could not be read: {ex.Message}";
            }
        }
    }

    public PompoTargetPlatform SelectedBuildPlatform
    {
        get => _selectedBuildPlatform;
        set
        {
            if (_selectedBuildPlatform == value)
            {
                return;
            }

            _selectedBuildPlatform = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProductionReadinessItems));
        }
    }

    public bool BuildReleaseCandidate
    {
        get => _buildReleaseCandidate;
        set
        {
            if (_buildReleaseCandidate == value)
            {
                return;
            }

            _buildReleaseCandidate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProductionReadinessItems));
        }
    }

    public IReadOnlyList<string> GraphOptions => State?.Graphs
        .Select(graph => graph.GraphId)
        .ToArray() ?? [];

    public string? SelectedGraphId
    {
        get => _selectedGraphId;
        set
        {
            if (_selectedGraphId == value)
            {
                return;
            }

            TrySelectGraphForEditing(value, allowDirtySwitch: false);
        }
    }

    public string GraphIdEdit
    {
        get => _graphIdEdit;
        set
        {
            if (_graphIdEdit == value)
            {
                return;
            }

            _graphIdEdit = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<EditorReadinessItem> ProductionReadinessItems => CreateProductionReadinessItems();

    public string ScenePlacementCharacterId
    {
        get => _scenePlacementCharacterId;
        set
        {
            if (_scenePlacementCharacterId == value)
            {
                return;
            }

            _scenePlacementCharacterId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ScenePlacementExpressionOptions));
        }
    }

    public string ScenePlacementExpressionId
    {
        get => _scenePlacementExpressionId;
        set
        {
            if (_scenePlacementExpressionId == value)
            {
                return;
            }

            _scenePlacementExpressionId = value;
            OnPropertyChanged();
        }
    }

    public RuntimeLayer ScenePlacementLayer
    {
        get => _scenePlacementLayer;
        set
        {
            if (_scenePlacementLayer == value)
            {
                return;
            }

            _scenePlacementLayer = value;
            OnPropertyChanged();
        }
    }

    public float ScenePlacementX
    {
        get => _scenePlacementX;
        set
        {
            if (Math.Abs(_scenePlacementX - value) < float.Epsilon)
            {
                return;
            }

            _scenePlacementX = value;
            OnPropertyChanged();
        }
    }

    public float ScenePlacementY
    {
        get => _scenePlacementY;
        set
        {
            if (Math.Abs(_scenePlacementY - value) < float.Epsilon)
            {
                return;
            }

            _scenePlacementY = value;
            OnPropertyChanged();
        }
    }

    public GraphEditorViewModel? GraphEditor
    {
        get => _graphEditor;
        private set
        {
            if (_graphEditor == value)
            {
                return;
            }

            _graphEditor = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<GraphNodeKind> AddableNodeKinds { get; } =
    [
        GraphNodeKind.Dialogue,
        GraphNodeKind.Narration,
        GraphNodeKind.Choice,
        GraphNodeKind.SetVariable,
        GraphNodeKind.Branch,
        GraphNodeKind.Jump,
        GraphNodeKind.CallGraph,
        GraphNodeKind.Return,
        GraphNodeKind.ShowCharacter,
        GraphNodeKind.HideCharacter,
        GraphNodeKind.MoveCharacter,
        GraphNodeKind.ChangeExpression,
        GraphNodeKind.PlayBgm,
        GraphNodeKind.StopBgm,
        GraphNodeKind.PlaySfx,
        GraphNodeKind.PlayVoice,
        GraphNodeKind.StopVoice,
        GraphNodeKind.ChangeBackground,
        GraphNodeKind.Fade,
        GraphNodeKind.Wait,
        GraphNodeKind.SavePoint,
        GraphNodeKind.UnlockCg,
        GraphNodeKind.EndScene
    ];

    public GraphNodeKind SelectedNodeKindToAdd
    {
        get => _selectedNodeKindToAdd;
        set
        {
            if (_selectedNodeKindToAdd == value)
            {
                return;
            }

            _selectedNodeKindToAdd = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<CustomNodePaletteItem> CustomNodePaletteItems
    {
        get => _customNodePaletteItems;
        private set
        {
            _customNodePaletteItems = value;
            OnPropertyChanged();
        }
    }

    public CustomNodePaletteItem? SelectedCustomNodeToAdd
    {
        get => _selectedCustomNodeToAdd;
        set
        {
            if (_selectedCustomNodeToAdd == value)
            {
                return;
            }

            _selectedCustomNodeToAdd = value;
            OnPropertyChanged();
        }
    }

    public string CustomNodePaletteStatus
    {
        get => _customNodePaletteStatus;
        private set
        {
            if (string.Equals(_customNodePaletteStatus, value, StringComparison.Ordinal))
            {
                return;
            }

            _customNodePaletteStatus = value;
            OnPropertyChanged();
        }
    }

    public GraphPreviewState Preview
    {
        get => _preview;
        private set
        {
            if (_preview == value)
            {
                return;
            }

            _preview = value;
            OnPropertyChanged();
        }
    }

    public async Task CreateSampleProjectAsync(
        string projectRoot,
        string projectName,
        CancellationToken cancellationToken = default)
    {
        await CreateProjectCoreAsync(
            projectRoot,
            projectName,
            "sample",
            () => _workspaceService.CreateSampleAsync(projectRoot, projectName, cancellationToken),
            cancellationToken);
    }

    public async Task CreateMinimalProjectAsync(
        string projectRoot,
        string projectName,
        CancellationToken cancellationToken = default)
    {
        await CreateProjectCoreAsync(
            projectRoot,
            projectName,
            "minimal",
            () => _workspaceService.CreateMinimalAsync(projectRoot, projectName, cancellationToken),
            cancellationToken);
    }

    public async Task LoadAsync(string projectRoot, CancellationToken cancellationToken = default)
    {
        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(await _workspaceService.LoadAsync(projectRoot, cancellationToken));
                await RefreshSaveSlotsCoreAsync(cancellationToken);
                await RefreshBuildHistoryCoreAsync(cancellationToken);
                await RecordCurrentProjectAsRecentAsync(cancellationToken);
                _doctorHasRun = false;
                DoctorDiagnostics = [];
                StatusMessage = Summary!.IsValid
                    ? $"Loaded '{Summary.ProjectName}'."
                    : $"Loaded '{Summary.ProjectName}' with {Summary.DiagnosticCount} diagnostics.";
            },
            cancellationToken);
    }

    public async Task RefreshRecentProjectsAsync(CancellationToken cancellationToken = default)
    {
        await RunWorkspaceOperationAsync(
            async () =>
            {
                await RefreshRecentProjectsCoreAsync(cancellationToken).ConfigureAwait(false);
                StatusMessage = RecentProjects.Count == 0
                    ? "No recent projects yet."
                    : $"Loaded {RecentProjects.Count} recent project(s).";
            },
            cancellationToken);
    }

    public async Task OpenSelectedRecentProjectAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedRecentProjectRoot is null)
        {
            StatusMessage = "Select a recent project before opening.";
            return;
        }

        if (!File.Exists(ProjectFileService.GetProjectFilePath(SelectedRecentProjectRoot)))
        {
            StatusMessage = $"Recent project is missing: {SelectedRecentProjectRoot}";
            await RefreshRecentProjectsCoreAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await LoadAsync(SelectedRecentProjectRoot, cancellationToken).ConfigureAwait(false);
    }

    public async Task ForgetSelectedRecentProjectAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedRecentProjectRoot is null)
        {
            StatusMessage = "Select a recent project before removing it.";
            return;
        }

        var removedRoot = SelectedRecentProjectRoot;
        await RunWorkspaceOperationAsync(
            async () =>
            {
                await _recentProjectsService.RemoveAsync(removedRoot, cancellationToken).ConfigureAwait(false);
                await RefreshRecentProjectsCoreAsync(cancellationToken).ConfigureAwait(false);
                StatusMessage = $"Removed recent project '{removedRoot}'.";
            },
            cancellationToken);
    }

    public async Task RunDoctorAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null)
        {
            StatusMessage = "Open a project before running doctor checks.";
            return;
        }

        await RunWorkspaceOperationAsync(
            async () =>
            {
                var result = await _workspaceService.RunDoctorAsync(ProjectRoot, cancellationToken)
                    .ConfigureAwait(false);
                _doctorHasRun = true;
                DoctorDiagnostics = result.Diagnostics
                    .Select(diagnostic => new EditorDiagnostic(
                        diagnostic.Code,
                        diagnostic.Message,
                        diagnostic.Path,
                        null))
                    .ToArray();
                StatusMessage = result.IsHealthy
                    ? "Doctor checks passed."
                    : $"Doctor checks found {result.Diagnostics.Count} issue(s).";
            },
            cancellationToken);
    }

    public async Task ValidateCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null)
        {
            StatusMessage = "Open a project before validating.";
            return;
        }

        await LoadAsync(ProjectRoot, cancellationToken);
    }

    public async Task RefreshSaveSlotsAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null)
        {
            StatusMessage = "Open a project before refreshing saves.";
            return;
        }

        await RunWorkspaceOperationAsync(
            async () =>
            {
                await RefreshSaveSlotsCoreAsync(cancellationToken);
                StatusMessage = $"Loaded {SaveSlots.Count} save slot(s).";
            },
            cancellationToken);
    }

    public async Task DeleteSelectedSaveSlotAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null)
        {
            StatusMessage = "Open a project before deleting saves.";
            return;
        }

        if (SelectedSaveSlotId is null)
        {
            StatusMessage = "Select a save slot before deleting.";
            return;
        }

        var deletedSlotId = SelectedSaveSlotId;
        await RunWorkspaceOperationAsync(
            async () =>
            {
                SaveSlots = await _workspaceService.DeleteSaveSlotAsync(ProjectRoot, deletedSlotId, cancellationToken);
                SelectedSaveSlotId = SaveSlots.FirstOrDefault()?.SlotId;
                StatusMessage = $"Deleted save slot '{deletedSlotId}'.";
            },
            cancellationToken);
    }

    public async Task BuildCurrentAsync(
        string outputRoot,
        string? profilePath = null,
        bool requireReleaseCandidate = false,
        PompoTargetPlatform? platformOverride = null,
        CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null)
        {
            StatusMessage = "Open a project before building.";
            return;
        }

        await RunWorkspaceOperationAsync(
            async () =>
            {
                var result = await _workspaceService.BuildAsync(
                    ProjectRoot,
                    outputRoot,
                    profilePath,
                    requireReleaseCandidate,
                    platformOverride,
                    cancellationToken);
                LastBuildOutputDirectory = result.OutputDirectory;
                LastBuildSummaryItems = CreateBuildSummaryItems(result.Manifest);
                BuildHistory = ToBuildHistoryItems(
                    await _buildHistoryService.RecordAsync(
                            ProjectRoot,
                            CreateBuildHistoryEntry(result, profilePath, requireReleaseCandidate, platformOverride),
                            cancellationToken)
                        .ConfigureAwait(false));
                LastReleaseArchivePath = null;
                LastReleaseManifestPath = null;
                ReleaseDiagnostics = [];
                BuildDiagnostics = result.Diagnostics
                    .Select(diagnostic => new EditorDiagnostic(
                        diagnostic.Code,
                        diagnostic.Message,
                        diagnostic.Path,
                        null))
                    .ToArray();
                StatusMessage = result.Success
                    ? $"Built project to '{result.OutputDirectory}'."
                    : $"Build failed with {result.Diagnostics.Count} diagnostics.";
            },
            cancellationToken);
    }

    public async Task SaveBuildProfileAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null || State is null)
        {
            StatusMessage = "Open a project before saving build profiles.";
            return;
        }

        var profileName = BuildProfileNameEdit.Trim();
        if (!IsValidDocumentId(profileName))
        {
            StatusMessage = "Build profile name can use letters, numbers, '-', '_', or '.'.";
            return;
        }

        var appName = string.IsNullOrWhiteSpace(BuildProfileAppName)
            ? State.Project.ProjectName
            : BuildProfileAppName.Trim();
        var version = string.IsNullOrWhiteSpace(BuildProfileVersion)
            ? State.Project.EngineVersion
            : BuildProfileVersion.Trim();
        var profile = new PompoBuildProfile(
            profileName,
            SelectedBuildPlatform,
            appName,
            version,
            PackageRuntime: BuildProfilePackageRuntime,
            RunSmokeTest: BuildProfileRunSmokeTest,
            SelfContained: BuildProfileSelfContained);

        await RunWorkspaceOperationAsync(
            async () =>
            {
                await _workspaceService.SaveBuildProfileAsync(ProjectRoot, profile, cancellationToken)
                    .ConfigureAwait(false);
                _selectedBuildProfileName = profileName;
                SyncBuildProfileFieldsFromSelection();
                OnPropertyChanged(nameof(BuildProfileOptions));
                OnPropertyChanged(nameof(SelectedBuildProfileName));
                OnPropertyChanged(nameof(SelectedBuildProfilePath));
                OnPropertyChanged(nameof(SelectedBuildProfileSummary));
                StatusMessage = $"Saved build profile '{profileName}'.";
            },
            cancellationToken);
    }

    public async Task SaveRuntimeThemeAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null)
        {
            StatusMessage = "Open a project before saving the runtime theme.";
            return;
        }

        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(await _workspaceService
                    .SaveRuntimeUiThemeAsync(
                        ProjectRoot,
                        CreateRuntimeUiThemeFromFields(),
                        CreateRuntimeUiSkinFromFields(),
                        CreateRuntimeUiLayoutFromFields(),
                        CreateRuntimeUiAnimationFromFields(),
                        CreateRuntimePlaybackFromFields(),
                        cancellationToken)
                    .ConfigureAwait(false));
                StatusMessage = "Saved runtime UI theme, skin, layout, animation, and playback.";
            },
            cancellationToken);
    }

    public void ResetRuntimeLayoutFields()
    {
        SyncRuntimeLayoutFields(new PompoRuntimeUiLayoutSettings());
        RaiseRuntimeThemePropertyChanges();
        StatusMessage = "Reset runtime UI layout fields to defaults. Save theme to persist.";
    }

    public void ApplyRuntimeAnimationPreset(string? presetId)
    {
        var preset = RuntimeAnimationPresets.FirstOrDefault(candidate =>
            string.Equals(candidate.PresetId, presetId, StringComparison.OrdinalIgnoreCase));
        if (preset is null)
        {
            StatusMessage = $"Runtime animation preset '{presetId}' was not found.";
            return;
        }

        SyncRuntimeAnimationFields(preset.Animation);
        SyncRuntimePlaybackFields(preset.Playback);
        RaiseRuntimeAnimationPropertyChanges();
        StatusMessage = $"Applied runtime animation preset '{preset.DisplayName}'. Save theme to persist.";
    }

    public async Task DeleteSelectedBuildProfileAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null)
        {
            StatusMessage = "Open a project before deleting build profiles.";
            return;
        }

        var profileName = SelectedBuildProfileName;
        if (string.IsNullOrWhiteSpace(profileName))
        {
            StatusMessage = "Select a build profile before deleting.";
            return;
        }

        await RunWorkspaceOperationAsync(
            async () =>
            {
                await _workspaceService.DeleteBuildProfileAsync(ProjectRoot, profileName, cancellationToken)
                    .ConfigureAwait(false);
                var nextProfile = BuildProfileOptions
                    .FirstOrDefault(profile => !string.Equals(profile, profileName, StringComparison.Ordinal)) ??
                    BuildProfileOptions.FirstOrDefault();
                if (nextProfile is not null)
                {
                    _selectedBuildProfileName = nextProfile;
                    SyncBuildProfileFieldsFromSelection();
                }

                OnPropertyChanged(nameof(BuildProfileOptions));
                OnPropertyChanged(nameof(SelectedBuildProfileName));
                OnPropertyChanged(nameof(SelectedBuildProfilePath));
                OnPropertyChanged(nameof(SelectedBuildProfileSummary));
                StatusMessage = $"Deleted build profile '{profileName}'.";
            },
            cancellationToken);
    }

    public Task BuildSelectedProfileAsync(
        string outputRoot,
        CancellationToken cancellationToken = default)
    {
        return BuildCurrentAsync(
            outputRoot,
            ResolveSelectedBuildProfilePath(),
            BuildReleaseCandidate,
            SelectedBuildPlatform,
            cancellationToken);
    }

    public async Task BuildAndPackageVerifiedReleaseAsync(
        string buildOutputRoot,
        string releaseOutputRoot,
        string? profilePath = null,
        PompoTargetPlatform? platformOverride = null,
        CancellationToken cancellationToken = default)
    {
        await BuildCurrentAsync(
            buildOutputRoot,
            profilePath,
            requireReleaseCandidate: true,
            platformOverride,
            cancellationToken);
        if (BuildDiagnostics.Count > 0)
        {
            return;
        }

        await PackageLastBuildAsync(releaseOutputRoot, requireSmokeTestedLocales: true, cancellationToken);
    }

    public Task BuildAndPackageSelectedProfileAsync(
        string buildOutputRoot,
        string releaseOutputRoot,
        CancellationToken cancellationToken = default)
    {
        return BuildAndPackageVerifiedReleaseAsync(
            buildOutputRoot,
            releaseOutputRoot,
            ResolveSelectedBuildProfilePath(),
            SelectedBuildPlatform,
            cancellationToken);
    }

    public async Task PackageLastBuildAsync(
        string releaseOutputRoot,
        bool requireSmokeTestedLocales = true,
        CancellationToken cancellationToken = default)
    {
        if (LastBuildOutputDirectory is null)
        {
            StatusMessage = "Build the project before packaging a release.";
            return;
        }

        await RunWorkspaceOperationAsync(
            async () =>
            {
                var result = await _workspaceService.PackageReleaseAsync(
                    LastBuildOutputDirectory,
                    releaseOutputRoot,
                    requireSmokeTestedLocales,
                    requireSelfContained: true,
                    cancellationToken);
                LastReleaseArchivePath = result.Manifest?.ArchivePath;
                LastReleaseManifestPath = result.Manifest is null
                    ? null
                    : Path.Combine(releaseOutputRoot, $"{result.Manifest.PackageName}.release.json");
                ReleaseDiagnostics = result.Diagnostics
                    .Select(diagnostic => new EditorDiagnostic(
                        diagnostic.Code,
                        diagnostic.Message,
                        diagnostic.Path,
                        null))
                    .ToArray();
                StatusMessage = result.IsValid
                    ? $"Packaged verified release '{result.Manifest!.PackageName}'."
                    : $"Release verification failed with {result.Diagnostics.Count} diagnostics.";
            },
            cancellationToken);
    }

    public async Task ImportAssetAsync(
        string sourceFile,
        CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null)
        {
            StatusMessage = "Open a project before importing assets.";
            return;
        }

        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(await _workspaceService.ImportAssetAsync(ProjectRoot, sourceFile, cancellationToken: cancellationToken));
                ResourceBrowser.SelectedResourceId = CreateAssetIdFromFileName(sourceFile);
                StatusMessage = $"Imported asset '{Path.GetFileName(sourceFile)}'.";
            },
            cancellationToken);
    }

    public async Task DeleteSelectedAssetAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null)
        {
            StatusMessage = "Open a project before deleting assets.";
            return;
        }

        var assetId = ResourceBrowser.SelectedResourceId;
        if (string.IsNullOrWhiteSpace(assetId))
        {
            StatusMessage = "Select an asset before deleting.";
            return;
        }

        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(await _workspaceService.DeleteAssetAsync(ProjectRoot, assetId, cancellationToken: cancellationToken));
                StatusMessage = $"Deleted asset '{assetId}'.";
            },
            cancellationToken);
    }

    public async Task FillMissingLocalizationValuesAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null)
        {
            StatusMessage = "Open a project before repairing localization.";
            return;
        }

        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(
                    await _workspaceService.FillMissingLocalizationValuesAsync(
                        ProjectRoot,
                        SelectedPreviewLocale,
                        cancellationToken));
                StatusMessage = "Filled missing localization values.";
            },
            cancellationToken);
    }

    public async Task AddSupportedLocaleAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null)
        {
            StatusMessage = "Open a project before editing locales.";
            return;
        }

        var locale = LocaleEdit.Trim();
        if (string.IsNullOrWhiteSpace(locale))
        {
            StatusMessage = "Enter a locale before adding it.";
            return;
        }

        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(await _workspaceService.AddSupportedLocaleAsync(ProjectRoot, locale, cancellationToken));
                SelectedPreviewLocale = locale;
                StatusMessage = $"Added locale '{locale}'.";
            },
            cancellationToken);
    }

    public async Task DeleteSelectedLocaleAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null)
        {
            StatusMessage = "Open a project before editing locales.";
            return;
        }

        var locale = SelectedPreviewLocale;
        if (string.IsNullOrWhiteSpace(locale))
        {
            StatusMessage = "Select a locale before deleting it.";
            return;
        }

        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(await _workspaceService.DeleteSupportedLocaleAsync(ProjectRoot, locale, cancellationToken));
                if (State is not null && !State.Project.SupportedLocales.Contains(SelectedPreviewLocale ?? string.Empty, StringComparer.Ordinal))
                {
                    SelectedPreviewLocale = State.Project.SupportedLocales.FirstOrDefault();
                }

                StatusMessage = $"Deleted locale '{locale}'.";
            },
            cancellationToken);
    }

    public void SelectLocalizationEntry(string tableId, string key)
    {
        LocalizationTableId = tableId;
        LocalizationKey = key;
        SyncLocalizationValueFromFields();
        OnPropertyChanged(nameof(LocalizationValue));
    }

    public async Task SaveLocalizationEntryAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null)
        {
            StatusMessage = "Open a project before editing localization.";
            return;
        }

        var locale = SelectedPreviewLocale;
        if (string.IsNullOrWhiteSpace(locale))
        {
            StatusMessage = "Select a preview locale before editing localization.";
            return;
        }

        var tableId = LocalizationTableId.Trim();
        var key = LocalizationKey.Trim();
        if (!IsValidDocumentId(tableId))
        {
            StatusMessage = "String table id can use letters, numbers, '-', '_', or '.'.";
            return;
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            StatusMessage = "String key is required.";
            return;
        }

        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(
                    await _workspaceService.UpsertLocalizationEntryAsync(
                        ProjectRoot,
                        tableId,
                        key,
                        locale,
                        LocalizationValue,
                        cancellationToken));
                _localizationTableId = tableId;
                _localizationKey = key;
                SyncLocalizationValueFromFields();
                OnPropertyChanged(nameof(LocalizationTableId));
                OnPropertyChanged(nameof(LocalizationKey));
                OnPropertyChanged(nameof(LocalizationValue));
                StatusMessage = $"Saved localization '{tableId}:{key}' for '{locale}'.";
            },
            cancellationToken);
    }

    public async Task DeleteLocalizationEntryAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null)
        {
            StatusMessage = "Open a project before editing localization.";
            return;
        }

        var tableId = LocalizationTableId.Trim();
        var key = LocalizationKey.Trim();
        if (string.IsNullOrWhiteSpace(tableId) || string.IsNullOrWhiteSpace(key))
        {
            StatusMessage = "Select or enter a localization entry before deleting.";
            return;
        }

        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(
                    await _workspaceService.DeleteLocalizationEntryAsync(
                        ProjectRoot,
                        tableId,
                        key,
                        cancellationToken));
                _localizationTableId = State?.Project.StringTables.FirstOrDefault()?.TableId ?? "ui";
                _localizationKey = State?.Project.StringTables.FirstOrDefault()?.Entries.FirstOrDefault()?.Key ?? string.Empty;
                SyncLocalizationValueFromFields();
                OnPropertyChanged(nameof(LocalizationTableId));
                OnPropertyChanged(nameof(LocalizationKey));
                OnPropertyChanged(nameof(LocalizationValue));
                StatusMessage = $"Deleted localization '{tableId}:{key}'.";
            },
            cancellationToken);
    }

    public async Task SaveSelectedSceneAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null || SelectedScene is null)
        {
            StatusMessage = "Open a project scene before saving.";
            return;
        }

        var sceneId = SelectedScene.SceneId;
        await RunWorkspaceOperationAsync(
            async () =>
            {
                var scene = CreateEditedScene(SelectedScene);
                ApplyState(
                    await _workspaceService.SaveSceneAsync(ProjectRoot, scene, cancellationToken),
                    preferredSceneId: sceneId);
                StatusMessage = $"Saved scene '{sceneId}'.";
            },
            cancellationToken);
    }

    public async Task AddSceneAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null || State is null)
        {
            StatusMessage = "Open a project before adding scenes.";
            return;
        }

        var sceneId = CreateUniqueSceneId();
        var scene = new SceneDefinition(
            sceneId,
            "New Scene",
            [],
            [],
            State.Project.Graphs.FirstOrDefault()?.GraphId ?? string.Empty);

        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(
                    await _workspaceService.AddSceneAsync(ProjectRoot, scene, cancellationToken),
                    preferredSceneId: sceneId);
                StatusMessage = $"Added scene '{sceneId}'.";
            },
            cancellationToken);
    }

    public async Task DeleteSelectedSceneAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null || SelectedScene is null)
        {
            StatusMessage = "Open a project scene before deleting scenes.";
            return;
        }

        if (State is null || State.Project.Scenes.Count <= 1)
        {
            StatusMessage = "Project must keep at least one scene.";
            return;
        }

        var deletedSceneId = SelectedScene.SceneId;
        var nextSceneId = State.Project.Scenes
            .FirstOrDefault(scene => !string.Equals(scene.SceneId, deletedSceneId, StringComparison.Ordinal))
            ?.SceneId;
        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(
                    await _workspaceService.DeleteSceneAsync(ProjectRoot, deletedSceneId, cancellationToken),
                    preferredSceneId: nextSceneId);
                StatusMessage = $"Deleted scene '{deletedSceneId}'.";
            },
            cancellationToken);
    }

    public async Task AddCharacterAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null || State is null)
        {
            StatusMessage = "Open a project before adding characters.";
            return;
        }

        var characterId = CreateUniqueCharacterId();
        var character = new CharacterDefinition(
            characterId,
            "New Character",
            null,
            []);

        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(
                    await _workspaceService.AddCharacterAsync(ProjectRoot, character, cancellationToken));
                SelectedCharacterId = characterId;
                StatusMessage = $"Added character '{characterId}'.";
            },
            cancellationToken);
    }

    public async Task SaveSelectedCharacterAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null || SelectedCharacter is null)
        {
            StatusMessage = "Select a project character before saving.";
            return;
        }

        var characterId = SelectedCharacter.CharacterId;
        var character = SelectedCharacter with
        {
            DisplayName = string.IsNullOrWhiteSpace(CharacterDisplayName)
                ? characterId
                : CharacterDisplayName.Trim(),
            DefaultExpression = string.IsNullOrWhiteSpace(CharacterDefaultExpression)
                ? null
                : CharacterDefaultExpression.Trim()
        };

        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(
                    await _workspaceService.SaveCharacterAsync(ProjectRoot, character, cancellationToken));
                SelectedCharacterId = characterId;
                StatusMessage = $"Saved character '{characterId}'.";
            },
            cancellationToken);
    }

    public async Task DeleteSelectedCharacterAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null || SelectedCharacter is null)
        {
            StatusMessage = "Select a project character before deleting.";
            return;
        }

        var deletedCharacterId = SelectedCharacter.CharacterId;
        var nextCharacterId = State?.Project.Characters
            .FirstOrDefault(character => !string.Equals(character.CharacterId, deletedCharacterId, StringComparison.Ordinal))
            ?.CharacterId;
        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(
                    await _workspaceService.DeleteCharacterAsync(ProjectRoot, deletedCharacterId, cancellationToken));
                SelectedCharacterId = nextCharacterId;
                StatusMessage = $"Deleted character '{deletedCharacterId}'.";
            },
            cancellationToken);
    }

    public async Task AddCharacterExpressionAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null || SelectedCharacter is null)
        {
            StatusMessage = "Select a project character before adding expressions.";
            return;
        }

        var spriteAssetId = CharacterExpressionSpriteAssetOptions.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(spriteAssetId))
        {
            StatusMessage = "Import an image asset before adding character expressions.";
            return;
        }

        var expressionId = CreateUniqueCharacterExpressionId();
        var expression = new CharacterExpression(
            expressionId,
            new PompoAssetRef(spriteAssetId, PompoAssetType.Image));
        var character = SelectedCharacter with
        {
            Expressions = SelectedCharacter.Expressions.Concat([expression]).ToArray(),
            DefaultExpression = SelectedCharacter.DefaultExpression ?? expressionId
        };

        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(
                    await _workspaceService.SaveCharacterAsync(ProjectRoot, character, cancellationToken));
                SelectedCharacterId = character.CharacterId;
                SelectedCharacterExpressionId = expressionId;
                StatusMessage = $"Added expression '{expressionId}'.";
            },
            cancellationToken);
    }

    public async Task SaveSelectedCharacterExpressionAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null || SelectedCharacter is null || SelectedCharacterExpression is null)
        {
            StatusMessage = "Select a character expression before saving.";
            return;
        }

        if (string.IsNullOrWhiteSpace(CharacterExpressionSpriteAssetId))
        {
            StatusMessage = "Select an image asset before saving an expression.";
            return;
        }

        var expressionId = SelectedCharacterExpression.ExpressionId;
        var characterId = SelectedCharacter.CharacterId;
        var expressions = SelectedCharacter.Expressions.Select(expression =>
            string.Equals(expression.ExpressionId, expressionId, StringComparison.Ordinal)
                ? expression with
                {
                    Sprite = new PompoAssetRef(CharacterExpressionSpriteAssetId.Trim(), PompoAssetType.Image),
                    Description = string.IsNullOrWhiteSpace(CharacterExpressionDescription)
                        ? null
                        : CharacterExpressionDescription.Trim()
                }
                : expression).ToArray();

        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(
                    await _workspaceService.SaveCharacterAsync(
                        ProjectRoot,
                        SelectedCharacter with { Expressions = expressions },
                        cancellationToken));
                SelectedCharacterId = characterId;
                SelectedCharacterExpressionId = expressionId;
                StatusMessage = $"Saved expression '{expressionId}'.";
            },
            cancellationToken);
    }

    public async Task DeleteSelectedCharacterExpressionAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null || SelectedCharacter is null || SelectedCharacterExpression is null)
        {
            StatusMessage = "Select a character expression before deleting.";
            return;
        }

        var expressionId = SelectedCharacterExpression.ExpressionId;
        if (string.Equals(SelectedCharacter.DefaultExpression, expressionId, StringComparison.Ordinal))
        {
            StatusMessage = "Cannot delete the default expression.";
            return;
        }

        if (State is not null && CharacterExpressionIsReferenced(State.Project, SelectedCharacter.CharacterId, expressionId))
        {
            StatusMessage = $"Expression '{expressionId}' is referenced by a scene or graph.";
            return;
        }

        var characterId = SelectedCharacter.CharacterId;
        var expressions = SelectedCharacter.Expressions
            .Where(expression => !string.Equals(expression.ExpressionId, expressionId, StringComparison.Ordinal))
            .ToArray();
        var nextExpressionId = expressions.FirstOrDefault()?.ExpressionId;

        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(
                    await _workspaceService.SaveCharacterAsync(
                        ProjectRoot,
                        SelectedCharacter with { Expressions = expressions },
                        cancellationToken));
                SelectedCharacterId = characterId;
                SelectedCharacterExpressionId = nextExpressionId;
                StatusMessage = $"Deleted expression '{expressionId}'.";
            },
            cancellationToken);
    }

    public async Task AddSceneCharacterPlacementAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null || SelectedScene is null)
        {
            StatusMessage = "Open a project scene before adding character placements.";
            return;
        }

        if (State is null || State.Project.Characters.Count == 0)
        {
            StatusMessage = "Add a project character before adding scene placements.";
            return;
        }

        var character = State.Project.Characters.First();
        var placementId = CreateUniqueScenePlacementId(character.CharacterId);
        var placement = new SceneCharacterPlacement(
            placementId,
            character.CharacterId,
            RuntimeLayer.Character,
            0.5f,
            1f,
            character.DefaultExpression);
        var scene = SelectedScene with { Characters = SelectedScene.Characters.Concat([placement]).ToArray() };

        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(
                    await _workspaceService.SaveSceneAsync(ProjectRoot, scene, cancellationToken),
                    preferredSceneId: scene.SceneId);
                SelectedScenePlacementId = placementId;
                StatusMessage = $"Added character placement '{placementId}'.";
            },
            cancellationToken);
    }

    public async Task DeleteSelectedSceneCharacterPlacementAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null || SelectedScene is null)
        {
            StatusMessage = "Open a project scene before deleting character placements.";
            return;
        }

        if (SelectedScenePlacementId is null)
        {
            StatusMessage = "Select a character placement before deleting.";
            return;
        }

        var deletedId = SelectedScenePlacementId;
        var scene = SelectedScene with
        {
            Characters = SelectedScene.Characters
                .Where(character => !string.Equals(character.PlacementId, deletedId, StringComparison.Ordinal))
                .ToArray()
        };

        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(
                    await _workspaceService.SaveSceneAsync(ProjectRoot, scene, cancellationToken),
                    preferredSceneId: scene.SceneId);
                StatusMessage = $"Deleted character placement '{deletedId}'.";
            },
            cancellationToken);
    }

    public void AddNodeToCurrentGraph(GraphNodeKind kind)
    {
        if (GraphEditor is null)
        {
            StatusMessage = "Open a project graph before adding nodes.";
            return;
        }

        var nodeId = CreateUniqueNodeId(kind);
        var offset = GraphEditor.Nodes.Count * 220;
        GraphEditor.AddNode(kind, nodeId, offset, 0);
        StatusMessage = $"Added {kind} node '{nodeId}'.";
    }

    public void AddSelectedNodeKindToCurrentGraph()
    {
        AddNodeToCurrentGraph(SelectedNodeKindToAdd);
    }

    public void AddSelectedCustomNodeToCurrentGraph()
    {
        if (GraphEditor is null)
        {
            StatusMessage = "Open a project graph before adding custom nodes.";
            return;
        }

        if (SelectedCustomNodeToAdd is null)
        {
            StatusMessage = "Select a custom script node before adding it.";
            return;
        }

        var nodeId = CreateUniqueNodeId(GraphNodeKind.Custom);
        var offset = GraphEditor.Nodes.Count * 220;
        GraphEditor.AddCustomNode(SelectedCustomNodeToAdd, nodeId, offset, 0);
        StatusMessage = $"Added custom node '{SelectedCustomNodeToAdd.DisplayName}'.";
    }

    public async Task AddGraphAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null || State is null)
        {
            StatusMessage = "Open a project before adding graphs.";
            return;
        }

        if (GraphEditor?.IsDirty == true)
        {
            StatusMessage = $"Save graph '{GraphEditor.GraphId}' before adding a new graph.";
            return;
        }

        var graphId = CreateUniqueGraphId();
        var authoring = new GraphAuthoringService();
        var graph = authoring.CreateEmpty(graphId);
        graph = authoring.AddNode(graph, GraphNodeKind.Start, "start", new GraphPoint(0, 0));
        graph = authoring.AddNode(graph, GraphNodeKind.EndScene, "end", new GraphPoint(260, 0));
        graph = authoring.Connect(graph, "start_to_end", "start", "out", "end", "in");

        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(
                    await _workspaceService.AddGraphAsync(ProjectRoot, graph, cancellationToken),
                    preferredGraphId: graphId);
                GraphEditor?.MarkSaved();
                StatusMessage = $"Added graph '{graphId}'.";
            },
            cancellationToken);
    }

    public async Task DeleteSelectedGraphAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null || SelectedGraphId is null)
        {
            StatusMessage = "Open a project graph before deleting graphs.";
            return;
        }

        if (GraphEditor?.IsDirty == true)
        {
            StatusMessage = $"Save graph '{GraphEditor.GraphId}' before deleting graphs.";
            return;
        }

        var deletedGraphId = SelectedGraphId;
        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(await _workspaceService.DeleteGraphAsync(ProjectRoot, deletedGraphId, cancellationToken));
                GraphEditor?.MarkSaved();
                StatusMessage = $"Deleted graph '{deletedGraphId}'.";
            },
            cancellationToken);
    }

    public async Task RenameSelectedGraphAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null || SelectedGraphId is null)
        {
            StatusMessage = "Open a project graph before renaming graphs.";
            return;
        }

        if (GraphEditor?.IsDirty == true)
        {
            StatusMessage = $"Save graph '{GraphEditor.GraphId}' before renaming graphs.";
            return;
        }

        var newGraphId = GraphIdEdit.Trim();
        if (!IsValidDocumentId(newGraphId))
        {
            StatusMessage = "Graph id can use letters, numbers, '-', '_', or '.'.";
            return;
        }

        if (string.Equals(SelectedGraphId, newGraphId, StringComparison.Ordinal))
        {
            StatusMessage = $"Graph '{SelectedGraphId}' already has that id.";
            return;
        }

        var oldGraphId = SelectedGraphId;
        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(
                    await _workspaceService.RenameGraphAsync(ProjectRoot, oldGraphId, newGraphId, cancellationToken),
                    preferredGraphId: newGraphId);
                GraphEditor?.MarkSaved();
                StatusMessage = $"Renamed graph '{oldGraphId}' to '{newGraphId}'.";
            },
            cancellationToken);
    }

    public void MarkSelectedGraphNodeAsConnectionSource()
    {
        if (GraphEditor is null)
        {
            StatusMessage = "Open a project graph before connecting nodes.";
            return;
        }

        GraphEditor.MarkSelectedNodeAsConnectionSource();
        StatusMessage = GraphEditor.ConnectionSourceNodeId is null
            ? "Select a graph node before setting a connection source."
            : $"Connection source set to '{GraphEditor.ConnectionSourceNodeId}'.";
    }

    public void ConnectGraphSourceToSelectedNode()
    {
        if (GraphEditor is null)
        {
            StatusMessage = "Open a project graph before connecting nodes.";
            return;
        }

        GraphEditor.ConnectConnectionSourceToSelectedNode();
        StatusMessage = GraphEditor.SelectedNodeId is null
            ? "Select a target node before connecting."
            : $"Connected to '{GraphEditor.SelectedNodeId}'.";
    }

    public void DisconnectSelectedGraphNodeEdges()
    {
        if (GraphEditor is null)
        {
            StatusMessage = "Open a project graph before disconnecting nodes.";
            return;
        }

        var removed = GraphEditor.DisconnectSelectedNodeEdges();
        StatusMessage = $"Disconnected {removed} edge(s).";
    }

    public void DeleteSelectedGraphNode()
    {
        if (GraphEditor is null)
        {
            StatusMessage = "Open a project graph before deleting nodes.";
            return;
        }

        var deletedNodeId = GraphEditor.SelectedNodeId;
        StatusMessage = GraphEditor.DeleteSelectedNode()
            ? $"Deleted graph node '{deletedNodeId}'."
            : "Select a graph node before deleting.";
    }

    public void DuplicateSelectedGraphNode()
    {
        if (GraphEditor is null)
        {
            StatusMessage = "Open a project graph before duplicating nodes.";
            return;
        }

        var duplicateNodeId = GraphEditor.DuplicateSelectedNode();
        StatusMessage = duplicateNodeId is null
            ? "Select a graph node before duplicating."
            : $"Duplicated graph node '{duplicateNodeId}'.";
    }

    public void UndoCurrentGraphEdit()
    {
        if (GraphEditor is null)
        {
            StatusMessage = "Open a project graph before undo.";
            return;
        }

        StatusMessage = GraphEditor.Undo()
            ? $"Undid edit in graph '{GraphEditor.GraphId}'."
            : "No graph edits to undo.";
    }

    public void RedoCurrentGraphEdit()
    {
        if (GraphEditor is null)
        {
            StatusMessage = "Open a project graph before redo.";
            return;
        }

        StatusMessage = GraphEditor.Redo()
            ? $"Redid edit in graph '{GraphEditor.GraphId}'."
            : "No graph edits to redo.";
    }

    public void RunCurrentGraphPreview()
    {
        RunCurrentGraphPreviewAsync().GetAwaiter().GetResult();
    }

    public async Task RunCurrentGraphPreviewAsync(CancellationToken cancellationToken = default)
    {
        if (GraphEditor is null)
        {
            Preview = new GraphPreviewState(
                false,
                "Open a graph before running preview.",
                [],
                new Dictionary<string, object?>(),
                "BGM: none; SFX: none",
                []);
            StatusMessage = Preview.Summary;
            return;
        }

        try
        {
            var graphLibrary = CompilePreviewGraphLibrary();
            var ir = graphLibrary[GraphEditor.GraphId];
            var result = await _previewRunner.RunAsync(
                    ir,
                    State?.Project,
                    SelectedPreviewLocale,
                    graphLibrary,
                    cancellationToken)
                .ConfigureAwait(false);
            Preview = new GraphPreviewState(
                result.Completed,
                result.Completed
                    ? $"Preview completed for '{result.GraphId}'."
                    : $"Preview stopped for '{result.GraphId}'.",
                result.Events.Select(ToPreviewEvent).ToArray(),
                result.Variables,
                FormatAudioSummary(result.Audio),
                []);
            StatusMessage = Preview.Summary;
        }
        catch (GraphCompilationException ex)
        {
            Preview = new GraphPreviewState(
                false,
                $"Preview failed: graph '{GraphEditor.GraphId}' cannot compile.",
                [],
                new Dictionary<string, object?>(),
                "BGM: none; SFX: none",
                ex.Diagnostics.Select(diagnostic => new EditorDiagnostic(
                    diagnostic.Code,
                    diagnostic.Message,
                    diagnostic.NodeId,
                    diagnostic.PortId)).ToArray());
            StatusMessage = Preview.Summary;
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or IOException or JsonException)
        {
            Preview = new GraphPreviewState(
                false,
                $"Preview failed: {ex.Message}",
                [],
                new Dictionary<string, object?>(),
                "BGM: none; SFX: none",
                [new EditorDiagnostic("PREVIEW001", ex.Message, GraphEditor.GraphId, null)]);
            StatusMessage = Preview.Summary;
        }
    }

    private IReadOnlyDictionary<string, PompoGraphIR> CompilePreviewGraphLibrary()
    {
        if (State is null || GraphEditor is null)
        {
            return new Dictionary<string, PompoGraphIR>(StringComparer.Ordinal);
        }

        var compiler = new GraphCompiler();
        return State.Graphs
            .Select(graph => string.Equals(graph.GraphId, GraphEditor.GraphId, StringComparison.Ordinal)
                ? GraphEditor.Graph
                : graph)
            .Select(compiler.Compile)
            .ToDictionary(ir => ir.GraphId, StringComparer.Ordinal);
    }

    public async Task SaveCurrentGraphAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectRoot is null || GraphEditor is null)
        {
            StatusMessage = "Open a project graph before saving.";
            return;
        }

        var graphId = GraphEditor.GraphId;
        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(
                    await _workspaceService.SaveGraphAsync(ProjectRoot, GraphEditor.Graph, cancellationToken),
                    graphId);
                GraphEditor?.MarkSaved();
                StatusMessage = $"Saved graph '{graphId}'.";
            },
            cancellationToken);
    }

    private void ApplyState(
        ProjectWorkspaceState state,
        string? preferredGraphId = null,
        string? preferredSceneId = null)
    {
        State = state;
        ResourceBrowser = new ResourceBrowserViewModel(State.Resources);
        OnPropertyChanged(nameof(ResourceBrowser));
        RefreshCustomNodePalette();
        if (SelectedPreviewLocale is null ||
            !State.Project.SupportedLocales.Contains(SelectedPreviewLocale, StringComparer.Ordinal))
        {
            SelectedPreviewLocale = State.Project.SupportedLocales.FirstOrDefault();
        }
        OnPropertyChanged(nameof(LocalizationLocales));
        OnPropertyChanged(nameof(LocalizationEntries));
        OnPropertyChanged(nameof(LocalizationDiagnostics));
        SyncLocalizationValueFromFields();
        OnPropertyChanged(nameof(SceneStartGraphOptions));
        OnPropertyChanged(nameof(SceneBackgroundAssetOptions));
        OnPropertyChanged(nameof(SceneCharacterOptions));
        SyncBuildProfileFieldsFromSelection();
        SyncRuntimeThemeFieldsFromState();

        var graph = preferredGraphId is null
            ? State.Graphs.FirstOrDefault(candidate => string.Equals(candidate.GraphId, SelectedGraphId, StringComparison.Ordinal)) ??
                State.Graphs.FirstOrDefault()
            : State.Graphs.FirstOrDefault(candidate => string.Equals(candidate.GraphId, preferredGraphId, StringComparison.Ordinal)) ??
                State.Graphs.FirstOrDefault();
        _selectedGraphId = graph?.GraphId;
        _graphIdEdit = graph?.GraphId ?? string.Empty;
        GraphEditor = graph is null ? null : new GraphEditorViewModel(graph);
        OnPropertyChanged(nameof(GraphOptions));
        OnPropertyChanged(nameof(SelectedGraphId));
        OnPropertyChanged(nameof(GraphIdEdit));

        var scene = preferredSceneId is null
            ? State.Project.Scenes.FirstOrDefault(scene => string.Equals(scene.SceneId, SelectedSceneId, StringComparison.Ordinal)) ??
                State.Project.Scenes.FirstOrDefault()
            : State.Project.Scenes.FirstOrDefault(candidate => string.Equals(candidate.SceneId, preferredSceneId, StringComparison.Ordinal)) ??
                State.Project.Scenes.FirstOrDefault();
        _selectedSceneId = scene?.SceneId;
        SyncSceneFieldsFromSelection();
        if (_selectedCharacterId is null ||
            State.Project.Characters.All(character => !string.Equals(character.CharacterId, _selectedCharacterId, StringComparison.Ordinal)))
        {
            _selectedCharacterId = State.Project.Characters.FirstOrDefault()?.CharacterId;
        }
        SyncCharacterFieldsFromSelection();
        OnPropertyChanged(nameof(SelectedSceneId));
        OnPropertyChanged(nameof(Scenes));
        OnPropertyChanged(nameof(SelectedScene));
        OnPropertyChanged(nameof(SceneLayerItems));
        OnPropertyChanged(nameof(SceneCharacterPlacements));
        OnPropertyChanged(nameof(SelectedCharacterId));
        OnPropertyChanged(nameof(Characters));
        OnPropertyChanged(nameof(SelectedCharacter));
        OnPropertyChanged(nameof(CharacterExpressions));
        OnPropertyChanged(nameof(SelectedCharacterExpression));
        OnPropertyChanged(nameof(CharacterExpressionSpriteAssetOptions));
        OnPropertyChanged(nameof(CharacterDefaultExpressionOptions));
        OnPropertyChanged(nameof(ProductionReadinessItems));
    }

    private bool TrySelectGraphForEditing(string? graphId, bool allowDirtySwitch)
    {
        if (State is null)
        {
            _selectedGraphId = null;
            _graphIdEdit = string.Empty;
            GraphEditor = null;
            OnPropertyChanged(nameof(SelectedGraphId));
            OnPropertyChanged(nameof(GraphIdEdit));
            return false;
        }

        if (!allowDirtySwitch && GraphEditor?.IsDirty == true)
        {
            StatusMessage = $"Save graph '{GraphEditor.GraphId}' before switching graphs.";
            OnPropertyChanged(nameof(SelectedGraphId));
            return false;
        }

        var graph = graphId is null
            ? State.Graphs.FirstOrDefault()
            : State.Graphs.FirstOrDefault(candidate => string.Equals(candidate.GraphId, graphId, StringComparison.Ordinal));
        if (graph is null)
        {
            StatusMessage = $"Graph '{graphId}' does not exist in the project.";
            OnPropertyChanged(nameof(SelectedGraphId));
            return false;
        }

        _selectedGraphId = graph.GraphId;
        _graphIdEdit = graph.GraphId;
        GraphEditor = new GraphEditorViewModel(graph);
        StatusMessage = $"Opened graph '{graph.GraphId}'.";
        OnPropertyChanged(nameof(SelectedGraphId));
        OnPropertyChanged(nameof(GraphIdEdit));
        return true;
    }

    private void RefreshCustomNodePalette()
    {
        if (ProjectRoot is null)
        {
            CustomNodePaletteItems = [];
            SelectedCustomNodeToAdd = null;
            CustomNodePaletteStatus = "Open a project to load custom script nodes.";
            return;
        }

        var scriptsRoot = Path.Combine(ProjectRoot, "Scripts");
        if (!Directory.Exists(scriptsRoot))
        {
            CustomNodePaletteItems = [];
            SelectedCustomNodeToAdd = null;
            CustomNodePaletteStatus = "No custom script nodes loaded.";
            return;
        }

        var sources = Directory
            .EnumerateFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToDictionary(
                path => Path.GetRelativePath(ProjectRoot, path).Replace('\\', '/'),
                File.ReadAllText,
                StringComparer.Ordinal);
        if (sources.Count == 0)
        {
            CustomNodePaletteItems = [];
            SelectedCustomNodeToAdd = null;
            CustomNodePaletteStatus = "No custom script nodes loaded.";
            return;
        }

        var permissions = State?.Project.ScriptPermissions ?? new PompoScriptPermissions();
        var result = new UserScriptCompiler().Compile(
            sources,
            new ScriptSecurityOptions(
                permissions.AllowFileSystem,
                permissions.AllowNetwork,
                permissions.AllowProcessExecution));
        if (!result.Success || result.AssemblyBytes is null)
        {
            CustomNodePaletteItems = [];
            SelectedCustomNodeToAdd = null;
            CustomNodePaletteStatus = $"Custom script nodes failed to compile: {result.Diagnostics.FirstOrDefault() ?? "unknown error"}";
            return;
        }

        var items = new CustomNodeDiscoveryService()
            .Discover(result.AssemblyBytes)
            .Select(descriptor => new CustomNodePaletteItem(
                descriptor.NodeType.FullName ?? descriptor.NodeType.Name,
                descriptor.DisplayName,
                typeof(PompoConditionNode).IsAssignableFrom(descriptor.NodeType),
                CreateDefaultCustomNodeProperties(descriptor.Inputs)))
            .OrderBy(item => item.DisplayName, StringComparer.Ordinal)
            .ToArray();
        CustomNodePaletteItems = items;
        SelectedCustomNodeToAdd = items.FirstOrDefault();
        CustomNodePaletteStatus = items.Length == 0
            ? "No custom script nodes loaded."
            : $"Loaded {items.Length} custom script node(s).";
    }

    private static IReadOnlyDictionary<string, JsonNode?> CreateDefaultCustomNodeProperties(
        IReadOnlyList<PompoNodeInputDescriptor> inputs)
    {
        return inputs.ToDictionary(
            input => ToCamelCase(input.Name),
            input => input.DefaultValue is null
                ? CreateDefaultCustomValue(input.ValueType)
                : JsonSerializer.SerializeToNode(input.DefaultValue, input.ValueType, ProjectFileService.CreateJsonOptions()),
            StringComparer.Ordinal);
    }

    private static JsonNode? CreateDefaultCustomValue(Type valueType)
    {
        var type = Nullable.GetUnderlyingType(valueType) ?? valueType;
        if (type == typeof(string))
        {
            return string.Empty;
        }

        if (type == typeof(bool))
        {
            return false;
        }

        if (type == typeof(int))
        {
            return 0;
        }

        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
        {
            return 0.0;
        }

        return null;
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || char.IsLower(value[0]))
        {
            return value;
        }

        return $"{char.ToLowerInvariant(value[0])}{value[1..]}";
    }

    private async Task CreateProjectCoreAsync(
        string projectRoot,
        string projectName,
        string templateName,
        Func<Task<ProjectWorkspaceState>> create,
        CancellationToken cancellationToken)
    {
        await RunWorkspaceOperationAsync(
            async () =>
            {
                ApplyState(await create().ConfigureAwait(false));
                await RefreshSaveSlotsCoreAsync(cancellationToken);
                await RefreshBuildHistoryCoreAsync(cancellationToken);
                await RecordCurrentProjectAsRecentAsync(cancellationToken);
                _doctorHasRun = false;
                LastBuildOutputDirectory = null;
                LastBuildSummaryItems = [];
                LastReleaseArchivePath = null;
                LastReleaseManifestPath = null;
                BuildDiagnostics = [];
                ReleaseDiagnostics = [];
                DoctorDiagnostics = [];
                StatusMessage = $"Created {templateName} project '{Summary!.ProjectName}'.";
            },
            cancellationToken);
    }

    private async Task RecordCurrentProjectAsRecentAsync(CancellationToken cancellationToken)
    {
        if (ProjectRoot is null || Summary is null)
        {
            return;
        }

        try
        {
            await _recentProjectsService
                .AddOrUpdateAsync(ProjectRoot, Summary.ProjectName, cancellationToken)
                .ConfigureAwait(false);
            await RefreshRecentProjectsCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            StatusMessage = $"Project loaded, but recent projects could not be updated: {ex.Message}";
        }
    }

    private async Task RefreshRecentProjectsCoreAsync(CancellationToken cancellationToken)
    {
        var projects = await _recentProjectsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var selectedRoot = SelectedRecentProjectRoot;
        RecentProjects = projects
            .Select(project =>
            {
                var root = Path.GetFullPath(project.ProjectRoot);
                return new RecentProjectViewItem(
                    project.ProjectName,
                    root,
                    project.LastOpenedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                    !File.Exists(ProjectFileService.GetProjectFilePath(root)),
                    string.Equals(root, selectedRoot, StringComparison.Ordinal));
            })
            .ToArray();

        if (RecentProjects.Count == 0)
        {
            _selectedRecentProjectRoot = null;
        }
        else if (_selectedRecentProjectRoot is null ||
            RecentProjects.All(project => !string.Equals(project.ProjectRoot, _selectedRecentProjectRoot, StringComparison.Ordinal)))
        {
            _selectedRecentProjectRoot = RecentProjects[0].ProjectRoot;
        }

        RefreshRecentProjectSelection();
        OnPropertyChanged(nameof(SelectedRecentProjectRoot));
        OnPropertyChanged(nameof(SelectedRecentProject));
    }

    private void RefreshRecentProjectSelection()
    {
        RecentProjects = RecentProjects
            .Select(project => project with
            {
                IsSelected = string.Equals(project.ProjectRoot, _selectedRecentProjectRoot, StringComparison.Ordinal)
            })
            .ToArray();
    }

    private async Task RefreshSaveSlotsCoreAsync(CancellationToken cancellationToken)
    {
        if (ProjectRoot is null)
        {
            SaveSlots = [];
            SelectedSaveSlotId = null;
            return;
        }

        SaveSlots = await _workspaceService.ListSaveSlotsAsync(ProjectRoot, cancellationToken).ConfigureAwait(false);
        if (SelectedSaveSlotId is null ||
            SaveSlots.All(slot => !string.Equals(slot.SlotId, SelectedSaveSlotId, StringComparison.Ordinal)))
        {
            SelectedSaveSlotId = SaveSlots.FirstOrDefault()?.SlotId;
        }
    }

    private async Task RefreshBuildHistoryCoreAsync(CancellationToken cancellationToken)
    {
        if (ProjectRoot is null)
        {
            BuildHistory = [];
            return;
        }

        BuildHistory = ToBuildHistoryItems(
            await _buildHistoryService.LoadAsync(ProjectRoot, cancellationToken).ConfigureAwait(false));
    }

    private static GraphPreviewEventItem ToPreviewEvent(RuntimeTraceEvent traceEvent)
    {
        return traceEvent.Kind switch
        {
            "line" => new GraphPreviewEventItem(
                "Line",
                traceEvent.Speaker is null ? traceEvent.Text ?? string.Empty : $"{traceEvent.Speaker}: {traceEvent.Text}",
                $"Instruction {traceEvent.InstructionPointer}"),
            "choice" => new GraphPreviewEventItem(
                "Choice",
                traceEvent.SelectedChoice ?? string.Empty,
                traceEvent.Choices is null ? string.Empty : string.Join(" / ", traceEvent.Choices)),
            "complete" => new GraphPreviewEventItem(
                "Complete",
                "Graph completed",
                $"Instruction {traceEvent.InstructionPointer}"),
            _ => new GraphPreviewEventItem(
                traceEvent.Kind,
                traceEvent.Text ?? string.Empty,
                $"Instruction {traceEvent.InstructionPointer}")
        };
    }

    private static string FormatAudioSummary(RuntimeAudioState audio)
    {
        var bgm = string.IsNullOrWhiteSpace(audio.BgmAssetId) ? "none" : audio.BgmAssetId;
        var sfx = audio.PlayingSfxAssetIds.Count == 0 ? "none" : string.Join(", ", audio.PlayingSfxAssetIds);
        var voice = string.IsNullOrWhiteSpace(audio.VoiceAssetId) ? "none" : audio.VoiceAssetId;
        return $"BGM: {bgm}; Voice: {voice}; SFX: {sfx}";
    }

    private static IReadOnlyList<BuildSummaryItem> CreateBuildSummaryItems(BuildArtifactManifest? manifest)
    {
        if (manifest is null)
        {
            return [];
        }

        return
        [
            new BuildSummaryItem("App", $"{manifest.AppName} {manifest.Version}"),
            new BuildSummaryItem("Platform", manifest.Platform.ToString()),
            new BuildSummaryItem("Runtime", manifest.SelfContained ? "self-contained" : "framework-dependent"),
            new BuildSummaryItem("Included files", manifest.IncludedFiles.Count.ToString()),
            new BuildSummaryItem("Compiled graphs", manifest.CompiledGraphs.Count.ToString()),
            new BuildSummaryItem("Supported locales", FormatBuildList(manifest.SupportedLocales)),
            new BuildSummaryItem("Smoke-tested locales", FormatBuildList(manifest.SmokeTestedLocales))
        ];
    }

    private BuildHistoryEntry CreateBuildHistoryEntry(
        BuildResult result,
        string? profilePath,
        bool requireReleaseCandidate,
        PompoTargetPlatform? platformOverride)
    {
        var profileName = ResolveBuildProfileName(profilePath, requireReleaseCandidate);
        return new BuildHistoryEntry(
            DateTimeOffset.UtcNow,
            profileName,
            result.Manifest?.Platform ?? platformOverride ?? SelectedBuildPlatform,
            result.OutputDirectory,
            result.Success,
            result.Diagnostics.Count,
            result.Manifest?.AppName ?? State?.Project.ProjectName ?? "Unknown",
            result.Manifest?.Version ?? State?.Project.EngineVersion ?? "0.1.0");
    }

    private string ResolveBuildProfileName(string? profilePath, bool requireReleaseCandidate)
    {
        if (!string.IsNullOrWhiteSpace(profilePath))
        {
            return Path.GetFileNameWithoutExtension(profilePath)
                .Replace(".pompo-build", string.Empty, StringComparison.Ordinal);
        }

        if (!string.IsNullOrWhiteSpace(SelectedBuildProfileName))
        {
            return SelectedBuildProfileName;
        }

        return requireReleaseCandidate ? "release" : "debug";
    }

    private static IReadOnlyList<BuildHistoryViewItem> ToBuildHistoryItems(
        IReadOnlyList<BuildHistoryEntry> entries)
    {
        return entries
            .Select(entry => new BuildHistoryViewItem(
                entry.BuiltAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                entry.ProfileName,
                entry.Platform.ToString(),
                entry.OutputDirectory,
                entry.Success ? "Success" : $"Failed ({entry.DiagnosticCount})",
                $"{entry.AppName} {entry.Version}"))
            .ToArray();
    }

    private static string FormatBuildList(IReadOnlyList<string> values)
    {
        return values.Count == 0 ? "none" : string.Join(", ", values);
    }

    private SceneDefinition CreateEditedScene(SceneDefinition scene)
    {
        var layers = scene.Layers
            .Where(layer => layer.Layer != RuntimeLayer.Background)
            .ToList();
        if (!string.IsNullOrWhiteSpace(SceneBackgroundAssetId))
        {
            layers.Insert(
                0,
                new SceneLayer(
                    "background",
                    RuntimeLayer.Background,
                new PompoAssetRef(SceneBackgroundAssetId, PompoAssetType.Image)));
        }

        var characters = scene.Characters.Select(character =>
        {
            if (!string.Equals(character.PlacementId, SelectedScenePlacementId, StringComparison.Ordinal))
            {
                return character;
            }

            return character with
            {
                CharacterId = ScenePlacementCharacterId.Trim(),
                Layer = ScenePlacementLayer,
                X = Math.Clamp(ScenePlacementX, 0f, 1f),
                Y = Math.Clamp(ScenePlacementY, 0f, 1.2f),
                InitialExpressionId = string.IsNullOrWhiteSpace(ScenePlacementExpressionId)
                    ? null
                    : ScenePlacementExpressionId.Trim()
            };
        }).ToList();

        return scene with
        {
            DisplayName = string.IsNullOrWhiteSpace(SceneDisplayName) ? scene.SceneId : SceneDisplayName.Trim(),
            StartGraphId = SceneStartGraphId.Trim(),
            Layers = layers,
            Characters = characters
        };
    }

    private void SyncSceneFieldsFromSelection()
    {
        var scene = SelectedScene;
        _sceneDisplayName = scene?.DisplayName ?? string.Empty;
        _sceneStartGraphId = scene?.StartGraphId ?? string.Empty;
        _sceneBackgroundAssetId = scene is null ? string.Empty : GetSceneBackgroundAssetId(scene);
        _selectedScenePlacementId = scene?.Characters.FirstOrDefault()?.PlacementId;
        SyncScenePlacementFieldsFromSelection();
        OnPropertyChanged(nameof(SceneDisplayName));
        OnPropertyChanged(nameof(SceneStartGraphId));
        OnPropertyChanged(nameof(SceneBackgroundAssetId));
        OnPropertyChanged(nameof(SelectedScenePlacementId));
    }

    private void SyncScenePlacementFieldsFromSelection()
    {
        var placement = SelectedScenePlacement;
        _scenePlacementCharacterId = placement?.CharacterId ?? SceneCharacterOptions.FirstOrDefault() ?? string.Empty;
        _scenePlacementExpressionId = placement?.InitialExpressionId ?? string.Empty;
        _scenePlacementLayer = placement?.Layer ?? RuntimeLayer.Character;
        _scenePlacementX = placement?.X ?? 0.5f;
        _scenePlacementY = placement?.Y ?? 1f;
        OnPropertyChanged(nameof(ScenePlacementCharacterId));
        OnPropertyChanged(nameof(ScenePlacementExpressionId));
        OnPropertyChanged(nameof(ScenePlacementLayer));
        OnPropertyChanged(nameof(ScenePlacementX));
        OnPropertyChanged(nameof(ScenePlacementY));
        OnPropertyChanged(nameof(ScenePlacementExpressionOptions));
    }

    private void SyncCharacterFieldsFromSelection()
    {
        var character = SelectedCharacter;
        _characterDisplayName = character?.DisplayName ?? string.Empty;
        _characterDefaultExpression = character?.DefaultExpression ?? string.Empty;
        _selectedCharacterExpressionId = character?.Expressions.FirstOrDefault()?.ExpressionId;
        SyncCharacterExpressionFieldsFromSelection();
        OnPropertyChanged(nameof(CharacterDisplayName));
        OnPropertyChanged(nameof(CharacterDefaultExpression));
        OnPropertyChanged(nameof(SelectedCharacterExpressionId));
        OnPropertyChanged(nameof(CharacterExpressions));
        OnPropertyChanged(nameof(SelectedCharacterExpression));
        OnPropertyChanged(nameof(CharacterDefaultExpressionOptions));
    }

    private void SyncCharacterExpressionFieldsFromSelection()
    {
        var expression = SelectedCharacterExpression;
        _characterExpressionSpriteAssetId = expression?.Sprite.AssetId ?? CharacterExpressionSpriteAssetOptions.FirstOrDefault() ?? string.Empty;
        _characterExpressionDescription = expression?.Description ?? string.Empty;
        OnPropertyChanged(nameof(CharacterExpressionSpriteAssetId));
        OnPropertyChanged(nameof(CharacterExpressionDescription));
    }

    private static string GetSceneBackgroundAssetId(SceneDefinition scene)
    {
        return scene.Layers.FirstOrDefault(layer => layer.Layer == RuntimeLayer.Background)?.Asset?.AssetId ?? string.Empty;
    }

    private IReadOnlyList<EditorReadinessItem> CreateProductionReadinessItems()
    {
        if (State is null || Summary is null)
        {
            return
            [
                new EditorReadinessItem(
                    "Project loaded",
                    "Missing",
                    "Create or open a Pompo project before release checks can run.",
                    IsPassing: false,
                    IsBlocking: true)
            ];
        }

        var releaseProfile = TryReadBuildProfile("release");
        var releaseProfileReady = releaseProfile is not null &&
            releaseProfile.PackageRuntime &&
            releaseProfile.RunSmokeTest &&
            releaseProfile.SelfContained;
        var releaseProfileDetail = releaseProfile is null
            ? "BuildProfiles/release.pompo-build.json is missing or invalid."
            : $"runtime={(releaseProfile.PackageRuntime ? "on" : "off")}, smoke={(releaseProfile.RunSmokeTest ? "on" : "off")}, self-contained={(releaseProfile.SelfContained ? "on" : "off")}";

        var selectedProfileExists = SelectedBuildProfilePath is not null && File.Exists(SelectedBuildProfilePath);
        var localizationReady = LocalizationDiagnostics.Count == 0;
        var doctorReady = _doctorHasRun && DoctorDiagnostics.Count == 0;

        return
        [
            new EditorReadinessItem(
                "Project loaded",
                "Ready",
                $"{Summary.ProjectName} at {Summary.ProjectRoot}",
                IsPassing: true,
                IsBlocking: false),
            new EditorReadinessItem(
                "Project validation",
                Summary.IsValid ? "Clean" : "Issues",
                Summary.IsValid
                    ? "Project schema, graph references, assets, and localization references are valid."
                    : $"{Summary.DiagnosticCount} diagnostic(s), {Summary.BrokenAssetCount} broken asset(s).",
                Summary.IsValid,
                IsBlocking: true),
            new EditorReadinessItem(
                "Graph content",
                Summary.GraphCount > 0 ? "Ready" : "Missing",
                $"{Summary.GraphCount} graph(s), {Summary.SceneCount} scene(s), {Summary.CharacterCount} character(s).",
                Summary.GraphCount > 0,
                IsBlocking: true),
            new EditorReadinessItem(
                "Localization",
                localizationReady ? "Ready" : "Issues",
                localizationReady
                    ? $"{State.Project.SupportedLocales.Count} supported locale(s) have no reported localization diagnostics."
                    : $"{LocalizationDiagnostics.Count} localization diagnostic(s) need review.",
                localizationReady,
                IsBlocking: true),
            new EditorReadinessItem(
                "Release profile",
                releaseProfileReady ? "Ready" : "Incomplete",
                releaseProfileDetail,
                releaseProfileReady,
                IsBlocking: true),
            new EditorReadinessItem(
                "Selected build profile",
                selectedProfileExists ? "Selected" : "Missing",
                selectedProfileExists
                    ? $"{SelectedBuildProfileName} -> {SelectedBuildPlatform}"
                    : "Choose an existing BuildProfiles/*.pompo-build.json file.",
                selectedProfileExists,
                IsBlocking: false),
            new EditorReadinessItem(
                "Project doctor",
                doctorReady ? "Passed" : _doctorHasRun ? "Issues" : "Not run",
                doctorReady
                    ? "Project doctor checks passed in this editor session."
                    : _doctorHasRun
                        ? $"{DoctorDiagnostics.Count} doctor diagnostic(s) need review."
                        : "Run Doctor before treating this project as release-ready.",
                doctorReady,
                IsBlocking: true)
        ];
    }

    private PompoBuildProfile? TryReadBuildProfile(string profileName)
    {
        if (ProjectRoot is null)
        {
            return null;
        }

        var path = BuildProfileFileService.GetDefaultProfilePath(ProjectRoot, profileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<PompoBuildProfile>(
                stream,
                ProjectFileService.CreateJsonOptions());
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        {
            return null;
        }
    }

    private void SyncRuntimeThemeFieldsFromState()
    {
        var theme = State?.Project.RuntimeUiTheme ?? new PompoRuntimeUiTheme();
        _runtimeThemeCanvasClear = theme.CanvasClear;
        _runtimeThemeStageFallback = theme.StageFallback;
        _runtimeThemeStageActiveFallback = theme.StageActiveFallback;
        _runtimeThemeDialogueBackground = theme.DialogueBackground;
        _runtimeThemeNameBoxBackground = theme.NameBoxBackground;
        _runtimeThemeChoiceBackground = theme.ChoiceBackground;
        _runtimeThemeChoiceSelectedBackground = theme.ChoiceSelectedBackground;
        _runtimeThemeSaveMenuBackground = theme.SaveMenuBackground;
        _runtimeThemeSaveSlotBackground = theme.SaveSlotBackground;
        _runtimeThemeSaveSlotEmptyBackground = theme.SaveSlotEmptyBackground;
        _runtimeThemeBacklogBackground = theme.BacklogBackground;
        _runtimeThemeText = theme.Text;
        _runtimeThemeMutedText = theme.MutedText;
        _runtimeThemeAccentText = theme.AccentText;
        _runtimeThemeHelpText = theme.HelpText;
        var skin = State?.Project.RuntimeUiSkin ?? new PompoRuntimeUiSkin();
        _runtimeSkinDialogueBoxAssetId = skin.DialogueBox?.AssetId ?? string.Empty;
        _runtimeSkinNameBoxAssetId = skin.NameBox?.AssetId ?? string.Empty;
        _runtimeSkinChoiceBoxAssetId = skin.ChoiceBox?.AssetId ?? string.Empty;
        _runtimeSkinChoiceSelectedBoxAssetId = skin.ChoiceSelectedBox?.AssetId ?? string.Empty;
        _runtimeSkinChoiceDisabledBoxAssetId = skin.ChoiceDisabledBox?.AssetId ?? string.Empty;
        _runtimeSkinSaveMenuPanelAssetId = skin.SaveMenuPanel?.AssetId ?? string.Empty;
        _runtimeSkinSaveSlotAssetId = skin.SaveSlot?.AssetId ?? string.Empty;
        _runtimeSkinSaveSlotSelectedAssetId = skin.SaveSlotSelected?.AssetId ?? string.Empty;
        _runtimeSkinSaveSlotEmptyAssetId = skin.SaveSlotEmpty?.AssetId ?? string.Empty;
        _runtimeSkinBacklogPanelAssetId = skin.BacklogPanel?.AssetId ?? string.Empty;
        SyncRuntimeLayoutFields(State?.Project.RuntimeUiLayout ?? new PompoRuntimeUiLayoutSettings());
        SyncRuntimeAnimationFields(State?.Project.RuntimeUiAnimation ?? new PompoRuntimeUiAnimationSettings());
        SyncRuntimePlaybackFields(State?.Project.RuntimePlayback ?? new PompoRuntimePlaybackSettings());
        RaiseRuntimeThemePropertyChanges();
    }

    private void SyncRuntimeLayoutFields(PompoRuntimeUiLayoutSettings layout)
    {
        _runtimeLayoutDialogueTextBoxX = layout.DialogueTextBox.X.ToString();
        _runtimeLayoutDialogueTextBoxY = layout.DialogueTextBox.Y.ToString();
        _runtimeLayoutDialogueTextBoxWidth = layout.DialogueTextBox.Width.ToString();
        _runtimeLayoutDialogueTextBoxHeight = layout.DialogueTextBox.Height.ToString();
        _runtimeLayoutDialogueNameBoxX = layout.DialogueNameBox.X.ToString();
        _runtimeLayoutDialogueNameBoxY = layout.DialogueNameBox.Y.ToString();
        _runtimeLayoutDialogueNameBoxWidth = layout.DialogueNameBox.Width.ToString();
        _runtimeLayoutDialogueNameBoxHeight = layout.DialogueNameBox.Height.ToString();
        _runtimeLayoutChoiceBoxWidth = layout.ChoiceBoxWidth.ToString();
        _runtimeLayoutChoiceBoxHeight = layout.ChoiceBoxHeight.ToString();
        _runtimeLayoutChoiceBoxSpacing = layout.ChoiceBoxSpacing.ToString();
        _runtimeLayoutSaveMenuX = layout.SaveMenuBounds.X.ToString();
        _runtimeLayoutSaveMenuY = layout.SaveMenuBounds.Y.ToString();
        _runtimeLayoutSaveMenuWidth = layout.SaveMenuBounds.Width.ToString();
        _runtimeLayoutSaveMenuHeight = layout.SaveMenuBounds.Height.ToString();
        _runtimeLayoutSaveSlotHeight = layout.SaveSlotHeight.ToString();
        _runtimeLayoutSaveSlotSpacing = layout.SaveSlotSpacing.ToString();
        _runtimeLayoutBacklogX = layout.BacklogBounds.X.ToString();
        _runtimeLayoutBacklogY = layout.BacklogBounds.Y.ToString();
        _runtimeLayoutBacklogWidth = layout.BacklogBounds.Width.ToString();
        _runtimeLayoutBacklogHeight = layout.BacklogBounds.Height.ToString();
    }

    private void SyncRuntimeAnimationFields(PompoRuntimeUiAnimationSettings animation)
    {
        _runtimeAnimationEnabled = animation.Enabled;
        _runtimeAnimationPanelFadeMilliseconds = animation.PanelFadeMilliseconds.ToString();
        _runtimeAnimationChoicePulseMilliseconds = animation.ChoicePulseMilliseconds.ToString();
        _runtimeAnimationChoicePulseStrength = animation.ChoicePulseStrength.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _runtimeAnimationTextRevealCharactersPerSecond = animation.TextRevealCharactersPerSecond.ToString();
    }

    private void SyncRuntimePlaybackFields(PompoRuntimePlaybackSettings playback)
    {
        _runtimePlaybackAutoForwardDelayMilliseconds = playback.AutoForwardDelayMilliseconds.ToString();
        _runtimePlaybackSkipIntervalMilliseconds = playback.SkipIntervalMilliseconds.ToString();
    }

    private bool PresetMatchesCurrentAnimation(RuntimeAnimationPreset preset)
    {
        return RuntimeAnimationEnabled == preset.Animation.Enabled &&
            string.Equals(RuntimeAnimationPanelFadeMilliseconds.Trim(), preset.Animation.PanelFadeMilliseconds.ToString(), StringComparison.Ordinal) &&
            string.Equals(RuntimeAnimationChoicePulseMilliseconds.Trim(), preset.Animation.ChoicePulseMilliseconds.ToString(), StringComparison.Ordinal) &&
            string.Equals(
                RuntimeAnimationChoicePulseStrength.Trim(),
                preset.Animation.ChoicePulseStrength.ToString(System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal) &&
            string.Equals(RuntimeAnimationTextRevealCharactersPerSecond.Trim(), preset.Animation.TextRevealCharactersPerSecond.ToString(), StringComparison.Ordinal) &&
            string.Equals(RuntimePlaybackAutoForwardDelayMilliseconds.Trim(), preset.Playback.AutoForwardDelayMilliseconds.ToString(), StringComparison.Ordinal) &&
            string.Equals(RuntimePlaybackSkipIntervalMilliseconds.Trim(), preset.Playback.SkipIntervalMilliseconds.ToString(), StringComparison.Ordinal);
    }

    private PompoRuntimeUiTheme CreateRuntimeUiThemeFromFields()
    {
        return new PompoRuntimeUiTheme(
            RuntimeThemeCanvasClear,
            RuntimeThemeStageFallback,
            RuntimeThemeStageActiveFallback,
            RuntimeThemeDialogueBackground,
            RuntimeThemeNameBoxBackground,
            RuntimeThemeChoiceBackground,
            RuntimeThemeChoiceSelectedBackground,
            RuntimeThemeSaveMenuBackground,
            RuntimeThemeSaveSlotBackground,
            RuntimeThemeSaveSlotEmptyBackground,
            RuntimeThemeBacklogBackground,
            RuntimeThemeText,
            RuntimeThemeMutedText,
            RuntimeThemeAccentText,
            RuntimeThemeHelpText);
    }

    private PompoRuntimeUiSkin CreateRuntimeUiSkinFromFields()
    {
        return new PompoRuntimeUiSkin(
            CreateRuntimeSkinAssetRef(RuntimeSkinDialogueBoxAssetId),
            CreateRuntimeSkinAssetRef(RuntimeSkinNameBoxAssetId),
            CreateRuntimeSkinAssetRef(RuntimeSkinChoiceBoxAssetId),
            CreateRuntimeSkinAssetRef(RuntimeSkinChoiceSelectedBoxAssetId),
            CreateRuntimeSkinAssetRef(RuntimeSkinSaveMenuPanelAssetId),
            CreateRuntimeSkinAssetRef(RuntimeSkinSaveSlotAssetId),
            CreateRuntimeSkinAssetRef(RuntimeSkinSaveSlotSelectedAssetId),
            CreateRuntimeSkinAssetRef(RuntimeSkinSaveSlotEmptyAssetId),
            CreateRuntimeSkinAssetRef(RuntimeSkinBacklogPanelAssetId),
            CreateRuntimeSkinAssetRef(RuntimeSkinChoiceDisabledBoxAssetId));
    }

    private static PompoAssetRef? CreateRuntimeSkinAssetRef(string assetId)
    {
        return string.IsNullOrWhiteSpace(assetId)
            ? null
            : new PompoAssetRef(assetId.Trim(), PompoAssetType.Image);
    }

    private PompoRuntimeUiLayoutSettings CreateRuntimeUiLayoutFromFields()
    {
        return new PompoRuntimeUiLayoutSettings
        {
            DialogueTextBox = CreateRuntimeLayoutRect(
                RuntimeLayoutDialogueTextBoxX,
                RuntimeLayoutDialogueTextBoxY,
                RuntimeLayoutDialogueTextBoxWidth,
                RuntimeLayoutDialogueTextBoxHeight,
                nameof(PompoRuntimeUiLayoutSettings.DialogueTextBox)),
            DialogueNameBox = CreateRuntimeLayoutRect(
                RuntimeLayoutDialogueNameBoxX,
                RuntimeLayoutDialogueNameBoxY,
                RuntimeLayoutDialogueNameBoxWidth,
                RuntimeLayoutDialogueNameBoxHeight,
                nameof(PompoRuntimeUiLayoutSettings.DialogueNameBox)),
            ChoiceBoxWidth = ParseRuntimeLayoutInt(RuntimeLayoutChoiceBoxWidth, nameof(PompoRuntimeUiLayoutSettings.ChoiceBoxWidth)),
            ChoiceBoxHeight = ParseRuntimeLayoutInt(RuntimeLayoutChoiceBoxHeight, nameof(PompoRuntimeUiLayoutSettings.ChoiceBoxHeight)),
            ChoiceBoxSpacing = ParseRuntimeLayoutInt(RuntimeLayoutChoiceBoxSpacing, nameof(PompoRuntimeUiLayoutSettings.ChoiceBoxSpacing)),
            SaveMenuBounds = CreateRuntimeLayoutRect(
                RuntimeLayoutSaveMenuX,
                RuntimeLayoutSaveMenuY,
                RuntimeLayoutSaveMenuWidth,
                RuntimeLayoutSaveMenuHeight,
                nameof(PompoRuntimeUiLayoutSettings.SaveMenuBounds)),
            SaveSlotHeight = ParseRuntimeLayoutInt(RuntimeLayoutSaveSlotHeight, nameof(PompoRuntimeUiLayoutSettings.SaveSlotHeight)),
            SaveSlotSpacing = ParseRuntimeLayoutInt(RuntimeLayoutSaveSlotSpacing, nameof(PompoRuntimeUiLayoutSettings.SaveSlotSpacing)),
            BacklogBounds = CreateRuntimeLayoutRect(
                RuntimeLayoutBacklogX,
                RuntimeLayoutBacklogY,
                RuntimeLayoutBacklogWidth,
                RuntimeLayoutBacklogHeight,
                nameof(PompoRuntimeUiLayoutSettings.BacklogBounds))
        };
    }

    private PompoRuntimeUiAnimationSettings CreateRuntimeUiAnimationFromFields()
    {
        return new PompoRuntimeUiAnimationSettings(
            RuntimeAnimationEnabled,
            ParseRuntimeLayoutInt(
                RuntimeAnimationPanelFadeMilliseconds,
                nameof(PompoRuntimeUiAnimationSettings.PanelFadeMilliseconds)),
            ParseRuntimeLayoutInt(
                RuntimeAnimationChoicePulseMilliseconds,
                nameof(PompoRuntimeUiAnimationSettings.ChoicePulseMilliseconds)),
            ParseRuntimeAnimationFloat(
                RuntimeAnimationChoicePulseStrength,
                nameof(PompoRuntimeUiAnimationSettings.ChoicePulseStrength)),
            ParseRuntimeLayoutInt(
                RuntimeAnimationTextRevealCharactersPerSecond,
                nameof(PompoRuntimeUiAnimationSettings.TextRevealCharactersPerSecond)));
    }

    private PompoRuntimePlaybackSettings CreateRuntimePlaybackFromFields()
    {
        return new PompoRuntimePlaybackSettings(
            ParseRuntimeLayoutInt(
                RuntimePlaybackAutoForwardDelayMilliseconds,
                nameof(PompoRuntimePlaybackSettings.AutoForwardDelayMilliseconds)),
            ParseRuntimeLayoutInt(
                RuntimePlaybackSkipIntervalMilliseconds,
                nameof(PompoRuntimePlaybackSettings.SkipIntervalMilliseconds)));
    }

    private static PompoRuntimeUiRect CreateRuntimeLayoutRect(
        string x,
        string y,
        string width,
        string height,
        string fieldName)
    {
        return new PompoRuntimeUiRect(
            ParseRuntimeLayoutInt(x, $"{fieldName}.X"),
            ParseRuntimeLayoutInt(y, $"{fieldName}.Y"),
            ParseRuntimeLayoutInt(width, $"{fieldName}.Width"),
            ParseRuntimeLayoutInt(height, $"{fieldName}.Height"));
    }

    private static int ParseRuntimeLayoutInt(string value, string fieldName)
    {
        if (!int.TryParse(value.Trim(), out var parsed))
        {
            throw new InvalidOperationException($"Runtime UI layout field '{fieldName}' must be an integer.");
        }

        return parsed;
    }

    private static float ParseRuntimeAnimationFloat(string value, string fieldName)
    {
        if (!float.TryParse(
                value.Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed))
        {
            throw new InvalidOperationException($"Runtime UI animation field '{fieldName}' must be a number.");
        }

        return parsed;
    }

    private bool SetRuntimeThemeField(
        ref string field,
        string value,
        [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        if (propertyName is not null &&
            (propertyName.StartsWith("RuntimeAnimation", StringComparison.Ordinal) ||
             propertyName.StartsWith("RuntimePlayback", StringComparison.Ordinal)))
        {
            OnPropertyChanged(nameof(RuntimeAnimationPresetSummary));
        }

        return true;
    }

    private void SetWorkspacePanelVisibility(
        ref bool field,
        bool value,
        string propertyName)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
        RaiseWorkspaceGridDefinitionChanges();
        StatusMessage = value
            ? $"Showed workspace panel '{FormatWorkspacePanelName(propertyName)}'."
            : $"Hid workspace panel '{FormatWorkspacePanelName(propertyName)}'.";
    }

    private void ApplyWorkspaceFocusState(
        string presetId,
        bool projectVisible,
        bool sceneVisible,
        bool graphVisible,
        bool inspectorVisible,
        bool consoleVisible)
    {
        if (!WorkspaceLayoutPresets.Any(preset =>
                string.Equals(preset.PresetId, presetId, StringComparison.Ordinal)))
        {
            presetId = "balanced";
        }

        if (!sceneVisible && !graphVisible)
        {
            graphVisible = true;
        }

        _selectedWorkspaceLayoutPresetId = presetId;
        _workspaceProjectPanelVisible = projectVisible;
        _workspaceScenePanelVisible = sceneVisible;
        _workspaceGraphPanelVisible = graphVisible;
        _workspaceInspectorPanelVisible = inspectorVisible;
        _workspaceConsolePanelVisible = consoleVisible;
        OnPropertyChanged(nameof(SelectedWorkspaceLayoutPresetId));
        OnPropertyChanged(nameof(SelectedWorkspaceLayoutPreset));
        RaiseWorkspacePanelVisibilityChanges();
    }

    private void ApplyWorkspacePreferences(EditorWorkspacePreferences preferences)
    {
        if (WorkspaceLayoutPresets.Any(preset =>
                string.Equals(preset.PresetId, preferences.SelectedPresetId, StringComparison.Ordinal)))
        {
            _selectedWorkspaceLayoutPresetId = preferences.SelectedPresetId;
        }

        _workspaceProjectPanelVisible = preferences.ProjectPanelVisible;
        _workspaceScenePanelVisible = preferences.ScenePanelVisible;
        _workspaceGraphPanelVisible = preferences.GraphPanelVisible;
        _workspaceInspectorPanelVisible = preferences.InspectorPanelVisible;
        _workspaceConsolePanelVisible = preferences.ConsolePanelVisible;
        if (!_workspaceScenePanelVisible && !_workspaceGraphPanelVisible)
        {
            _workspaceGraphPanelVisible = true;
        }

        OnPropertyChanged(nameof(SelectedWorkspaceLayoutPresetId));
        OnPropertyChanged(nameof(SelectedWorkspaceLayoutPreset));
        RaiseWorkspacePanelVisibilityChanges();
    }

    private void RaiseWorkspacePanelVisibilityChanges()
    {
        OnPropertyChanged(nameof(WorkspaceProjectPanelVisible));
        OnPropertyChanged(nameof(WorkspaceScenePanelVisible));
        OnPropertyChanged(nameof(WorkspaceGraphPanelVisible));
        OnPropertyChanged(nameof(WorkspaceInspectorPanelVisible));
        OnPropertyChanged(nameof(WorkspaceConsolePanelVisible));
        RaiseWorkspaceGridDefinitionChanges();
    }

    private void RaiseWorkspaceGridDefinitionChanges()
    {
        OnPropertyChanged(nameof(WorkspaceColumnDefinitions));
        OnPropertyChanged(nameof(WorkspaceRowDefinitions));
        OnPropertyChanged(nameof(WorkspaceRightPanelVisible));
        OnPropertyChanged(nameof(WorkspaceCenterSplitterVisible));
    }

    private static string FormatWorkspacePanelName(string propertyName)
    {
        return propertyName
            .Replace("Workspace", string.Empty, StringComparison.Ordinal)
            .Replace("PanelVisible", string.Empty, StringComparison.Ordinal);
    }

    private void RaiseRuntimeThemePropertyChanges()
    {
        OnPropertyChanged(nameof(RuntimeThemeCanvasClear));
        OnPropertyChanged(nameof(RuntimeThemeStageFallback));
        OnPropertyChanged(nameof(RuntimeThemeStageActiveFallback));
        OnPropertyChanged(nameof(RuntimeThemeDialogueBackground));
        OnPropertyChanged(nameof(RuntimeThemeNameBoxBackground));
        OnPropertyChanged(nameof(RuntimeThemeChoiceBackground));
        OnPropertyChanged(nameof(RuntimeThemeChoiceSelectedBackground));
        OnPropertyChanged(nameof(RuntimeThemeSaveMenuBackground));
        OnPropertyChanged(nameof(RuntimeThemeSaveSlotBackground));
        OnPropertyChanged(nameof(RuntimeThemeSaveSlotEmptyBackground));
        OnPropertyChanged(nameof(RuntimeThemeBacklogBackground));
        OnPropertyChanged(nameof(RuntimeThemeText));
        OnPropertyChanged(nameof(RuntimeThemeMutedText));
        OnPropertyChanged(nameof(RuntimeThemeAccentText));
        OnPropertyChanged(nameof(RuntimeThemeHelpText));
        OnPropertyChanged(nameof(RuntimeSkinDialogueBoxAssetId));
        OnPropertyChanged(nameof(RuntimeSkinNameBoxAssetId));
        OnPropertyChanged(nameof(RuntimeSkinChoiceBoxAssetId));
        OnPropertyChanged(nameof(RuntimeSkinChoiceSelectedBoxAssetId));
        OnPropertyChanged(nameof(RuntimeSkinChoiceDisabledBoxAssetId));
        OnPropertyChanged(nameof(RuntimeSkinSaveMenuPanelAssetId));
        OnPropertyChanged(nameof(RuntimeSkinSaveSlotAssetId));
        OnPropertyChanged(nameof(RuntimeSkinSaveSlotSelectedAssetId));
        OnPropertyChanged(nameof(RuntimeSkinSaveSlotEmptyAssetId));
        OnPropertyChanged(nameof(RuntimeSkinBacklogPanelAssetId));
        OnPropertyChanged(nameof(RuntimeLayoutDialogueTextBoxX));
        OnPropertyChanged(nameof(RuntimeLayoutDialogueTextBoxY));
        OnPropertyChanged(nameof(RuntimeLayoutDialogueTextBoxWidth));
        OnPropertyChanged(nameof(RuntimeLayoutDialogueTextBoxHeight));
        OnPropertyChanged(nameof(RuntimeLayoutDialogueNameBoxX));
        OnPropertyChanged(nameof(RuntimeLayoutDialogueNameBoxY));
        OnPropertyChanged(nameof(RuntimeLayoutDialogueNameBoxWidth));
        OnPropertyChanged(nameof(RuntimeLayoutDialogueNameBoxHeight));
        OnPropertyChanged(nameof(RuntimeLayoutChoiceBoxWidth));
        OnPropertyChanged(nameof(RuntimeLayoutChoiceBoxHeight));
        OnPropertyChanged(nameof(RuntimeLayoutChoiceBoxSpacing));
        OnPropertyChanged(nameof(RuntimeLayoutSaveMenuX));
        OnPropertyChanged(nameof(RuntimeLayoutSaveMenuY));
        OnPropertyChanged(nameof(RuntimeLayoutSaveMenuWidth));
        OnPropertyChanged(nameof(RuntimeLayoutSaveMenuHeight));
        OnPropertyChanged(nameof(RuntimeLayoutSaveSlotHeight));
        OnPropertyChanged(nameof(RuntimeLayoutSaveSlotSpacing));
        OnPropertyChanged(nameof(RuntimeLayoutBacklogX));
        OnPropertyChanged(nameof(RuntimeLayoutBacklogY));
        OnPropertyChanged(nameof(RuntimeLayoutBacklogWidth));
        OnPropertyChanged(nameof(RuntimeLayoutBacklogHeight));
        OnPropertyChanged(nameof(RuntimeAnimationEnabled));
        OnPropertyChanged(nameof(RuntimeAnimationPanelFadeMilliseconds));
        OnPropertyChanged(nameof(RuntimeAnimationChoicePulseMilliseconds));
        OnPropertyChanged(nameof(RuntimeAnimationChoicePulseStrength));
        OnPropertyChanged(nameof(RuntimeAnimationTextRevealCharactersPerSecond));
        OnPropertyChanged(nameof(RuntimePlaybackAutoForwardDelayMilliseconds));
        OnPropertyChanged(nameof(RuntimePlaybackSkipIntervalMilliseconds));
        OnPropertyChanged(nameof(RuntimeAnimationPresetSummary));
    }

    private void RaiseRuntimeAnimationPropertyChanges()
    {
        OnPropertyChanged(nameof(RuntimeAnimationEnabled));
        OnPropertyChanged(nameof(RuntimeAnimationPanelFadeMilliseconds));
        OnPropertyChanged(nameof(RuntimeAnimationChoicePulseMilliseconds));
        OnPropertyChanged(nameof(RuntimeAnimationChoicePulseStrength));
        OnPropertyChanged(nameof(RuntimeAnimationTextRevealCharactersPerSecond));
        OnPropertyChanged(nameof(RuntimePlaybackAutoForwardDelayMilliseconds));
        OnPropertyChanged(nameof(RuntimePlaybackSkipIntervalMilliseconds));
        OnPropertyChanged(nameof(RuntimeAnimationPresetSummary));
    }

    private void SyncBuildProfileFieldsFromSelection()
    {
        _buildProfileNameEdit = SelectedBuildProfileName;
        var profile = TryReadBuildProfile(SelectedBuildProfileName);
        if (profile is null)
        {
            _buildProfileAppName = State?.Project.ProjectName ?? string.Empty;
            _buildProfileVersion = State?.Project.EngineVersion ?? "0.1.0";
            _buildProfilePackageRuntime = true;
            _buildProfileRunSmokeTest = false;
            _buildProfileSelfContained = false;
        }
        else
        {
            _buildProfileAppName = profile.AppName;
            _buildProfileVersion = profile.Version;
            _buildProfilePackageRuntime = profile.PackageRuntime;
            _buildProfileRunSmokeTest = profile.RunSmokeTest;
            _buildProfileSelfContained = profile.SelfContained;
            _selectedBuildPlatform = profile.Platform;
        }

        OnPropertyChanged(nameof(BuildProfileNameEdit));
        OnPropertyChanged(nameof(BuildProfileAppName));
        OnPropertyChanged(nameof(BuildProfileVersion));
        OnPropertyChanged(nameof(BuildProfilePackageRuntime));
        OnPropertyChanged(nameof(BuildProfileRunSmokeTest));
        OnPropertyChanged(nameof(BuildProfileSelfContained));
        OnPropertyChanged(nameof(SelectedBuildPlatform));
    }

    private string CreateUniqueNodeId(GraphNodeKind kind)
    {
        var prefix = kind.ToString()
            .Select(character => char.IsUpper(character) ? $"-{char.ToLowerInvariant(character)}" : character.ToString());
        var normalizedPrefix = string.Join(string.Empty, prefix).Trim('-');
        var index = GraphEditor!.Nodes.Count + 1;
        string candidate;
        do
        {
            candidate = $"{normalizedPrefix}_{index++}";
        }
        while (GraphEditor.Nodes.Any(node => string.Equals(node.NodeId, candidate, StringComparison.Ordinal)));

        return candidate;
    }

    private string CreateUniqueGraphId()
    {
        var existing = State?.Graphs
            .Select(graph => graph.GraphId)
            .ToHashSet(StringComparer.Ordinal) ?? [];
        var index = existing.Count + 1;
        string candidate;
        do
        {
            candidate = $"graph_{index++}";
        }
        while (existing.Contains(candidate));

        return candidate;
    }

    private static bool IsValidDocumentId(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length > 0 &&
            trimmed is not "." and not ".." &&
            trimmed.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');
    }

    private string? ResolveSelectedBuildProfilePath()
    {
        if (ProjectRoot is null || string.IsNullOrWhiteSpace(SelectedBuildProfileName))
        {
            return null;
        }

        return BuildProfileFileService.GetDefaultProfilePath(ProjectRoot, SelectedBuildProfileName);
    }

    private string CreateUniqueSceneId()
    {
        var existing = State?.Project.Scenes
            .Select(scene => scene.SceneId)
            .ToHashSet(StringComparer.Ordinal) ?? [];
        var index = existing.Count + 1;
        string candidate;
        do
        {
            candidate = $"scene_{index++}";
        }
        while (existing.Contains(candidate));

        return candidate;
    }

    private string CreateUniqueCharacterId()
    {
        var existing = State?.Project.Characters
            .Select(character => character.CharacterId)
            .ToHashSet(StringComparer.Ordinal) ?? [];
        var index = existing.Count + 1;
        string candidate;
        do
        {
            candidate = $"character_{index++}";
        }
        while (existing.Contains(candidate));

        return candidate;
    }

    private string CreateUniqueCharacterExpressionId()
    {
        var existing = SelectedCharacter?.Expressions
            .Select(expression => expression.ExpressionId)
            .ToHashSet(StringComparer.Ordinal) ?? [];
        var index = existing.Count + 1;
        string candidate;
        do
        {
            candidate = $"expression_{index++}";
        }
        while (existing.Contains(candidate));

        return candidate;
    }

    private static bool CharacterExpressionIsReferenced(
        PompoProjectDocument project,
        string characterId,
        string expressionId)
    {
        var sceneReference = project.Scenes
            .SelectMany(scene => scene.Characters)
            .Any(placement =>
                string.Equals(placement.CharacterId, characterId, StringComparison.Ordinal) &&
                string.Equals(placement.InitialExpressionId, expressionId, StringComparison.Ordinal));
        if (sceneReference)
        {
            return true;
        }

        return project.Graphs
            .SelectMany(graph => graph.Nodes)
            .Any(node =>
                NodeStringPropertyEquals(node.Properties, "characterId", characterId) &&
                (NodeStringPropertyEquals(node.Properties, "expressionId", expressionId) ||
                    NodeStringPropertyEquals(node.Properties, "expression", expressionId)));
    }

    private static bool NodeStringPropertyEquals(JsonObject properties, string key, string value)
    {
        return properties.TryGetPropertyValue(key, out var node) &&
            node is not null &&
            node.GetValueKind() == JsonValueKind.String &&
            string.Equals(node.GetValue<string>(), value, StringComparison.Ordinal);
    }

    private string CreateUniqueScenePlacementId(string characterId)
    {
        var prefix = new string(characterId
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray())
            .Trim('_');
        if (string.IsNullOrWhiteSpace(prefix))
        {
            prefix = "character";
        }

        var existing = SelectedScene?.Characters
            .Select(character => character.PlacementId)
            .ToHashSet(StringComparer.Ordinal) ?? [];
        var index = 1;
        var candidate = prefix;
        while (existing.Contains(candidate))
        {
            candidate = $"{prefix}_{++index}";
        }

        return candidate;
    }

    private static string CreateAssetIdFromFileName(string sourceFile)
    {
        var name = Path.GetFileNameWithoutExtension(sourceFile)
            .Trim()
            .ToLowerInvariant();
        var sanitized = new string(name.Select(character =>
            char.IsLetterOrDigit(character) ? character : '-').ToArray());
        return string.Join('-', sanitized.Split('-', StringSplitOptions.RemoveEmptyEntries));
    }

    private IReadOnlyList<LocalizationStringEntryItem> CreateLocalizationEntries()
    {
        if (State is null)
        {
            return [];
        }

        var supportedLocales = State.Project.SupportedLocales
            .Where(locale => !string.IsNullOrWhiteSpace(locale))
            .ToArray();
        var supportedLookup = supportedLocales.ToHashSet(StringComparer.Ordinal);
        return State.Project.StringTables
            .SelectMany(table => table.Entries.Select(entry =>
            {
                var missing = supportedLocales.Any(locale => !entry.Values.ContainsKey(locale));
                var unsupported = entry.Values.Keys.Any(locale => !supportedLookup.Contains(locale));
                var orderedValues = supportedLocales
                    .Where(entry.Values.ContainsKey)
                    .Concat(entry.Values.Keys
                        .Where(locale => !supportedLookup.Contains(locale))
                        .Order(StringComparer.Ordinal))
                    .Select(locale => $"{locale}={entry.Values[locale]}");
                return new LocalizationStringEntryItem(
                    table.TableId,
                    entry.Key,
                    string.Join("; ", orderedValues),
                    missing,
                    unsupported);
            }))
            .OrderBy(entry => entry.TableId, StringComparer.Ordinal)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .ToArray();
    }

    private void SyncLocalizationValueFromFields()
    {
        if (State is null || string.IsNullOrWhiteSpace(SelectedPreviewLocale))
        {
            _localizationValue = string.Empty;
            OnPropertyChanged(nameof(LocalizationValue));
            return;
        }

        var table = State.Project.StringTables.FirstOrDefault(candidate =>
            string.Equals(candidate.TableId, LocalizationTableId.Trim(), StringComparison.Ordinal));
        var entry = table?.Entries.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, LocalizationKey.Trim(), StringComparison.Ordinal));
        _localizationValue = entry is not null &&
            entry.Values.TryGetValue(SelectedPreviewLocale, out var value)
                ? value
                : string.Empty;
        OnPropertyChanged(nameof(LocalizationValue));
    }

    private static bool IsLocalizationDiagnosticCode(string code)
    {
        return code is
            "POMPO016" or
            "POMPO017" or
            "POMPO018" or
            "POMPO019" or
            "POMPO020" or
            "POMPO021" or
            "POMPO022" or
            "POMPO023" or
            "POMPO024" or
            "POMPO025";
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

    private async Task RunWorkspaceOperationAsync(
        Func<Task> operation,
        CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            await operation();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
        {
            StatusMessage = ex.Message;
            throw;
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsBusy = false;
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
