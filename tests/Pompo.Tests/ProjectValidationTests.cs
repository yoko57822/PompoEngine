using System.Text.Json.Nodes;
using Pompo.Core.Assets;
using Pompo.Core.Characters;
using Pompo.Core.Graphs;
using Pompo.Core.Localization;
using Pompo.Core.Project;
using Pompo.Core.Scenes;

namespace Pompo.Tests;

public sealed class ProjectValidationTests
{
    [Fact]
    public void Validate_ReportsMissingGraphInventory()
    {
        var project = new PompoProjectDocument
        {
            ProjectName = "Empty Project",
            Graphs = []
        };

        var result = new ProjectValidator().Validate(project);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO026");
    }

    [Fact]
    public void Validate_ReportsDuplicateProjectDocumentIds()
    {
        var project = new PompoProjectDocument
        {
            ProjectName = "Duplicate IDs",
            Scenes =
            [
                new SceneDefinition("intro", "Intro", [], [], string.Empty),
                new SceneDefinition("intro", "Intro Copy", [], [], string.Empty)
            ],
            Characters =
            [
                new CharacterDefinition("mina", "Mina", null, []),
                new CharacterDefinition("mina", "Mina Copy", null, [])
            ],
            Graphs =
            [
                new GraphDocument(1, "graph_intro", [], []),
                new GraphDocument(1, "graph_intro", [], [])
            ]
        };

        var result = new ProjectValidator().Validate(project);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO029" && diagnostic.ElementId == "intro");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO030" && diagnostic.ElementId == "mina");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO031" && diagnostic.ElementId == "graph_intro");
    }

    [Theory]
    [InlineData("../graph")]
    [InlineData(".graph")]
    [InlineData("graph.")]
    [InlineData("bad graph")]
    public void Validate_ReportsUnsafeGraphIds(string graphId)
    {
        var project = new PompoProjectDocument
        {
            ProjectName = "Unsafe Graph",
            Graphs = [new GraphDocument(1, graphId, [], [])]
        };

        var result = new ProjectValidator().Validate(project);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO037" && diagnostic.ElementId == graphId);
    }

    [Fact]
    public void Validate_ReportsCharacterExpressionDiagnostics()
    {
        var project = new PompoProjectDocument
        {
            ProjectName = "Broken Character Expressions",
            Assets = new AssetDatabase
            {
                Assets =
                [
                    new AssetMetadata(
                        "mina-smile",
                        "Assets/Images/mina-smile.png",
                        PompoAssetType.Image,
                        new AssetImportOptions(),
                        "hash",
                        [])
                ]
            },
            Characters =
            [
                new CharacterDefinition(
                    "mina",
                    "Mina",
                    "angry",
                    [
                        new CharacterExpression("smile", new PompoAssetRef("mina-smile", PompoAssetType.Image)),
                        new CharacterExpression("smile", new PompoAssetRef("mina-smile", PompoAssetType.Image)),
                        new CharacterExpression("", new PompoAssetRef("mina-smile", PompoAssetType.Image))
                    ])
            ],
            Graphs = [GraphFixtures.LinearGraph()]
        };

        var result = new ProjectValidator().Validate(project);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO032" && diagnostic.ElementId == "smile");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO033" && diagnostic.ElementId == "angry");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO034" && diagnostic.DocumentPath == "mina");
    }

    [Fact]
    public void Validate_ReportsUnsafeAssetIds()
    {
        var project = new PompoProjectDocument
        {
            ProjectName = "Unsafe Assets",
            Assets = new AssetDatabase
            {
                Assets =
                [
                    new AssetMetadata(
                        "../outside",
                        "Assets/Images/outside.png",
                        PompoAssetType.Image,
                        new AssetImportOptions(),
                        "hash",
                        [])
                ]
            },
            Graphs = [GraphFixtures.LinearGraph()]
        };

        var result = new ProjectValidator().Validate(project);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO035" && diagnostic.ElementId == "../outside");
    }

    [Fact]
    public void Validate_ReportsUnsafeAssetSourcePaths()
    {
        var project = new PompoProjectDocument
        {
            ProjectName = "Unsafe Asset Paths",
            Assets = new AssetDatabase
            {
                Assets =
                [
                    new AssetMetadata(
                        "outside",
                        "../outside.png",
                        PompoAssetType.Image,
                        new AssetImportOptions(),
                        "hash",
                        [])
                ]
            },
            Graphs = [GraphFixtures.LinearGraph()]
        };

        var result = new ProjectValidator().Validate(project);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO036" && diagnostic.ElementId == "outside");
    }

    [Fact]
    public void Validate_ReportsGraphNodeReferenceDiagnostics()
    {
        var project = new PompoProjectDocument
        {
            ProjectName = "Broken Graph References",
            Assets = new AssetDatabase
            {
                Assets =
                [
                    new AssetMetadata(
                        "theme",
                        "Assets/Audio/theme.wav",
                        PompoAssetType.Audio,
                        new AssetImportOptions(),
                        "hash",
                        []),
                    new AssetMetadata(
                        "mina-smile",
                        "Assets/Images/mina-smile.png",
                        PompoAssetType.Image,
                        new AssetImportOptions(),
                        "hash",
                        [])
                ]
            },
            Characters =
            [
                new CharacterDefinition(
                    "mina",
                    "Mina",
                    "smile",
                    [new CharacterExpression("smile", new PompoAssetRef("mina-smile", PompoAssetType.Image))])
            ],
            Graphs =
            [
                new GraphDocument(
                    1,
                    "intro",
                    [
                        Node("missing_asset", GraphNodeKind.PlaySfx, new JsonObject { ["assetId"] = "missing-sfx" }),
                        Node("wrong_asset_type", GraphNodeKind.ChangeBackground, new JsonObject { ["assetId"] = "theme" }),
                        Node("missing_character", GraphNodeKind.ShowCharacter, new JsonObject { ["characterId"] = "riley" }),
                        Node("missing_expression", GraphNodeKind.ChangeExpression, new JsonObject { ["characterId"] = "mina", ["expressionId"] = "angry" }),
                        Node("missing_call", GraphNodeKind.CallGraph, new JsonObject { ["graphId"] = "credits" }),
                        Node("missing_jump", GraphNodeKind.Jump, new JsonObject { ["targetNodeId"] = "missing_node" })
                    ],
                    [])
            ]
        };

        var result = new ProjectValidator().Validate(project);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO011" && diagnostic.ElementId == "missing_asset");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO012" && diagnostic.ElementId == "wrong_asset_type");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO013" && diagnostic.ElementId == "missing_character");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO014" && diagnostic.ElementId == "missing_expression");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO015" && diagnostic.ElementId == "missing_call");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO028" && diagnostic.ElementId == "missing_jump");
    }

    [Fact]
    public void Validate_ReportsLocalizationDiagnostics()
    {
        var project = new PompoProjectDocument
        {
            ProjectName = "Localized",
            SupportedLocales = ["ko", "en", "ko"],
            StringTables =
            [
                new StringTableDocument(
                    "ui",
                    [
                        new StringTableEntry(
                            "start",
                            new Dictionary<string, string>
                            {
                                ["ko"] = "시작",
                                ["jp"] = "start"
                            }),
                        new StringTableEntry(
                            "start",
                            new Dictionary<string, string>
                            {
                                ["ko"] = "다시 시작",
                                ["en"] = "Start again"
                            })
                    ]),
                new StringTableDocument("ui", [])
            ]
        };

        var result = new ProjectValidator().Validate(project);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO017" && diagnostic.ElementId == "ko");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO019" && diagnostic.DocumentPath == "ui");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO021" && diagnostic.ElementId == "start");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO022" && diagnostic.ElementId == "start");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO023" && diagnostic.ElementId == "start");
    }

    [Fact]
    public void Validate_ReportsRuntimeUiThemeColorDiagnostics()
    {
        var project = new PompoProjectDocument
        {
            ProjectName = "Bad Theme",
            Graphs = [GraphFixtures.LinearGraph()],
            RuntimeUiTheme = new PompoRuntimeUiTheme(
                CanvasClear: "#102030",
                DialogueBackground: "112233",
                Text: "#FFFFFG")
        };

        var result = new ProjectValidator().Validate(project);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "POMPO038" &&
            diagnostic.DocumentPath == "runtimeUiTheme" &&
            diagnostic.ElementId == nameof(PompoRuntimeUiTheme.DialogueBackground));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "POMPO038" &&
            diagnostic.ElementId == nameof(PompoRuntimeUiTheme.Text));
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code == "POMPO038" &&
            diagnostic.ElementId == nameof(PompoRuntimeUiTheme.CanvasClear));
    }

    [Fact]
    public void Validate_ReportsRuntimeUiSkinAssetDiagnostics()
    {
        var project = new PompoProjectDocument
        {
            ProjectName = "Bad Skin",
            Graphs = [GraphFixtures.LinearGraph()],
            Assets = new AssetDatabase
            {
                Assets =
                [
                    new AssetMetadata(
                        "voice-click",
                        "Assets/Audio/voice-click.wav",
                        PompoAssetType.Audio,
                        new AssetImportOptions(),
                        "hash",
                        [])
                ]
            },
            RuntimeUiSkin = new PompoRuntimeUiSkin(
                DialogueBox: new PompoAssetRef("missing-dialogue", PompoAssetType.Image),
                ChoiceBox: new PompoAssetRef("voice-click", PompoAssetType.Image),
                ChoiceDisabledBox: new PompoAssetRef("missing-disabled", PompoAssetType.Image))
        };

        var result = new ProjectValidator().Validate(project);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "POMPO004" &&
            diagnostic.DocumentPath == "runtimeUiSkin" &&
            diagnostic.ElementId == nameof(PompoRuntimeUiSkin.DialogueBox));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "POMPO005" &&
            diagnostic.DocumentPath == "runtimeUiSkin" &&
            diagnostic.ElementId == nameof(PompoRuntimeUiSkin.ChoiceBox));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "POMPO004" &&
            diagnostic.DocumentPath == "runtimeUiSkin" &&
            diagnostic.ElementId == nameof(PompoRuntimeUiSkin.ChoiceDisabledBox));
    }

    [Fact]
    public void Validate_ReportsRuntimeUiLayoutDiagnostics()
    {
        var project = new PompoProjectDocument
        {
            ProjectName = "Bad Layout",
            Graphs = [GraphFixtures.LinearGraph()],
            RuntimeUiLayout = new PompoRuntimeUiLayoutSettings
            {
                DialogueTextBox = new PompoRuntimeUiRect(1900, 900, 100, 100),
                ChoiceBoxHeight = 0,
                SaveSlotSpacing = -1
            }
        };

        var result = new ProjectValidator().Validate(project);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "POMPO039" &&
            diagnostic.DocumentPath == "runtimeUiLayout" &&
            diagnostic.ElementId == nameof(PompoRuntimeUiLayoutSettings.DialogueTextBox));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "POMPO039" &&
            diagnostic.ElementId == nameof(PompoRuntimeUiLayoutSettings.ChoiceBoxHeight));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "POMPO039" &&
            diagnostic.ElementId == nameof(PompoRuntimeUiLayoutSettings.SaveSlotSpacing));
    }

    [Fact]
    public void Validate_ReportsRuntimeUiAnimationDiagnostics()
    {
        var project = new PompoProjectDocument
        {
            ProjectName = "Bad Animation",
            Graphs = [GraphFixtures.LinearGraph()],
            RuntimeUiAnimation = new PompoRuntimeUiAnimationSettings(
                PanelFadeMilliseconds: -1,
                ChoicePulseMilliseconds: -10,
                ChoicePulseStrength: 1.5f,
                TextRevealCharactersPerSecond: -30)
        };

        var result = new ProjectValidator().Validate(project);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "POMPO040" &&
            diagnostic.DocumentPath == "runtimeUiAnimation" &&
            diagnostic.ElementId == nameof(PompoRuntimeUiAnimationSettings.PanelFadeMilliseconds));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "POMPO040" &&
            diagnostic.ElementId == nameof(PompoRuntimeUiAnimationSettings.ChoicePulseMilliseconds));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "POMPO041" &&
            diagnostic.ElementId == nameof(PompoRuntimeUiAnimationSettings.ChoicePulseStrength));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "POMPO040" &&
            diagnostic.ElementId == nameof(PompoRuntimeUiAnimationSettings.TextRevealCharactersPerSecond));
    }

    [Fact]
    public void Validate_ReportsRuntimePlaybackDiagnostics()
    {
        var project = new PompoProjectDocument
        {
            ProjectName = "Bad Playback",
            Graphs = [GraphFixtures.LinearGraph()],
            RuntimePlayback = new PompoRuntimePlaybackSettings(
                AutoForwardDelayMilliseconds: -1,
                SkipIntervalMilliseconds: -10)
        };

        var result = new ProjectValidator().Validate(project);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "POMPO042" &&
            diagnostic.DocumentPath == "runtimePlayback" &&
            diagnostic.ElementId == nameof(PompoRuntimePlaybackSettings.AutoForwardDelayMilliseconds));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "POMPO042" &&
            diagnostic.ElementId == nameof(PompoRuntimePlaybackSettings.SkipIntervalMilliseconds));
    }

    [Fact]
    public void Validate_ReportsLocalizedGraphTextReferenceDiagnostics()
    {
        var project = new PompoProjectDocument
        {
            ProjectName = "Localized Graph",
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
            Graphs =
            [
                new GraphDocument(
                    1,
                    "intro",
                    [
                        Node("missing_table", GraphNodeKind.Narration, new JsonObject { ["tableId"] = "missing", ["textKey"] = "line.hello" }),
                        Node("missing_key", GraphNodeKind.Dialogue, new JsonObject { ["tableId"] = "dialogue", ["textKey"] = "line.missing" }),
                        Node(
                            "missing_choice_key",
                            GraphNodeKind.Choice,
                            new JsonObject
                            {
                                ["tableId"] = "dialogue",
                                ["choices"] = new JsonArray
                                {
                                    new JsonObject { ["textKey"] = "choice.missing", ["port"] = "next" }
                                }
                            })
                    ],
                    [])
            ]
        };

        var result = new ProjectValidator().Validate(project);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO024" && diagnostic.ElementId == "missing_table");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO025" && diagnostic.ElementId == "missing_key");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO025" && diagnostic.ElementId == "missing_choice_key");
    }

    [Fact]
    public void Validate_ReportsInvalidChoiceStateProperties()
    {
        var project = new PompoProjectDocument
        {
            ProjectName = "Invalid Choice State",
            Graphs =
            [
                new GraphDocument(
                    1,
                    "intro",
                    [
                        Node(
                            "choice",
                            GraphNodeKind.Choice,
                            new JsonObject
                            {
                                ["choices"] = new JsonArray
                                {
                                    new JsonObject { ["text"] = "Bad enabled", ["port"] = "next", ["enabled"] = "yes" },
                                    new JsonObject { ["text"] = "Bad variable", ["port"] = "next", ["enabledVariable"] = 42 }
                                }
                            })
                    ],
                    [])
            ]
        };

        var result = new ProjectValidator().Validate(project);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Diagnostics.Count(diagnostic => diagnostic.Code == "POMPO043" && diagnostic.ElementId == "choice"));
    }

    [Fact]
    public void Validate_ReportsGraphCallCycles()
    {
        var project = new PompoProjectDocument
        {
            ProjectName = "Cyclic Calls",
            Graphs =
            [
                new GraphDocument(
                    1,
                    "intro",
                    [Node("call_cafe", GraphNodeKind.CallGraph, new JsonObject { ["graphId"] = "cafe" })],
                    []),
                new GraphDocument(
                    1,
                    "cafe",
                    [Node("call_rooftop", GraphNodeKind.CallGraph, new JsonObject { ["targetGraphId"] = "rooftop" })],
                    []),
                new GraphDocument(
                    1,
                    "rooftop",
                    [Node("call_intro", GraphNodeKind.CallGraph, new JsonObject { ["graphId"] = "intro" })],
                    [])
            ]
        };

        var result = new ProjectValidator().Validate(project);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "POMPO027" &&
            diagnostic.DocumentPath == "rooftop" &&
            diagnostic.ElementId == "call_intro" &&
            diagnostic.Message.Contains("intro -> cafe -> rooftop -> intro", StringComparison.Ordinal));
    }

    private static GraphNode Node(string id, GraphNodeKind kind, JsonObject properties)
    {
        return new GraphNode(id, kind, new GraphPoint(0, 0), [], properties);
    }
}
