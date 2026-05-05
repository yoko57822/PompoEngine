using Pompo.Core.Assets;
using Pompo.Core.Characters;
using Pompo.Core.Graphs;
using Pompo.Core.Localization;
using Pompo.Core.Scenes;

namespace Pompo.Core.Project;

public sealed record PompoProjectDocument
{
    public int SchemaVersion { get; init; } = ProjectConstants.CurrentSchemaVersion;
    public Guid ProjectId { get; init; } = Guid.NewGuid();
    public required string ProjectName { get; init; }
    public string EngineVersion { get; init; } = "0.1.0";
    public int VirtualWidth { get; init; } = 1920;
    public int VirtualHeight { get; init; } = 1080;
    public List<string> SupportedLocales { get; init; } = ["ko", "en"];
    public AssetDatabase Assets { get; init; } = new();
    public List<SceneDefinition> Scenes { get; init; } = [];
    public List<CharacterDefinition> Characters { get; init; } = [];
    public List<GraphDocument> Graphs { get; init; } = [];
    public List<StringTableDocument> StringTables { get; init; } = [];
    public PompoScriptPermissions ScriptPermissions { get; init; } = new();
    public PompoRuntimeUiTheme RuntimeUiTheme { get; init; } = new();
    public PompoRuntimeUiSkin RuntimeUiSkin { get; init; } = new();
    public PompoRuntimeUiLayoutSettings RuntimeUiLayout { get; init; } = new();
    public PompoRuntimeUiAnimationSettings RuntimeUiAnimation { get; init; } = new();
    public PompoRuntimePlaybackSettings RuntimePlayback { get; init; } = new();
    public string? StartSceneId { get; init; }
}

public sealed record PompoScriptPermissions(
    bool AllowFileSystem = false,
    bool AllowNetwork = false,
    bool AllowProcessExecution = false);

public sealed record PompoRuntimeUiTheme(
    string CanvasClear = "#181A20",
    string StageFallback = "#262B36",
    string StageActiveFallback = "#314967",
    string DialogueBackground = "#0C0E12DC",
    string NameBoxBackground = "#48546E",
    string ChoiceBackground = "#192130E6",
    string ChoiceSelectedBackground = "#2563EBEB",
    string SaveMenuBackground = "#0A0E18EB",
    string SaveSlotBackground = "#1E293BEB",
    string SaveSlotEmptyBackground = "#0F172ADC",
    string BacklogBackground = "#080C14F2",
    string Text = "#FFFFFF",
    string MutedText = "#CBD5E1",
    string AccentText = "#93C5FD",
    string HelpText = "#94A3B8");

public sealed record PompoRuntimeUiSkin(
    PompoAssetRef? DialogueBox = null,
    PompoAssetRef? NameBox = null,
    PompoAssetRef? ChoiceBox = null,
    PompoAssetRef? ChoiceSelectedBox = null,
    PompoAssetRef? SaveMenuPanel = null,
    PompoAssetRef? SaveSlot = null,
    PompoAssetRef? SaveSlotSelected = null,
    PompoAssetRef? SaveSlotEmpty = null,
    PompoAssetRef? BacklogPanel = null,
    PompoAssetRef? ChoiceDisabledBox = null);

public sealed record PompoRuntimeUiRect(int X, int Y, int Width, int Height);

public sealed record PompoRuntimeUiLayoutSettings
{
    public PompoRuntimeUiRect DialogueTextBox { get; init; } = new(120, 810, 1680, 190);
    public PompoRuntimeUiRect DialogueNameBox { get; init; } = new(150, 755, 420, 54);
    public int ChoiceBoxWidth { get; init; } = 720;
    public int ChoiceBoxHeight { get; init; } = 56;
    public int ChoiceBoxSpacing { get; init; } = 14;
    public PompoRuntimeUiRect SaveMenuBounds { get; init; } = new(1260, 60, 560, 720);
    public int SaveSlotHeight { get; init; } = 60;
    public int SaveSlotSpacing { get; init; } = 10;
    public PompoRuntimeUiRect BacklogBounds { get; init; } = new(260, 120, 1400, 840);
}

public sealed record PompoRuntimeUiAnimationSettings(
    bool Enabled = true,
    int PanelFadeMilliseconds = 160,
    int ChoicePulseMilliseconds = 900,
    float ChoicePulseStrength = 0.12f,
    int TextRevealCharactersPerSecond = 45);

public sealed record PompoRuntimePlaybackSettings(
    int AutoForwardDelayMilliseconds = 1250,
    int SkipIntervalMilliseconds = 80);
