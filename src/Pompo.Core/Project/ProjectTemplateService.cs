using System.Text.Json.Nodes;
using System.Text.Json;
using Pompo.Core.Assets;
using Pompo.Core.Characters;
using Pompo.Core.Graphs;
using Pompo.Core.Localization;
using Pompo.Core.Runtime;
using Pompo.Core.Scenes;

namespace Pompo.Core.Project;

public sealed class ProjectTemplateService
{
    private readonly ProjectFileService _projectFiles = new();

    public async Task<PompoProjectDocument> CreateMinimalVisualNovelAsync(
        string projectRoot,
        string projectName,
        CancellationToken cancellationToken = default)
    {
        var project = new PompoProjectDocument
        {
            ProjectName = projectName,
            StartSceneId = "scene_intro",
            StringTables = [CreateDefaultStringTable()],
            Scenes =
            [
                new SceneDefinition(
                    "scene_intro",
                    "Intro",
                    [],
                    [],
                    "graph_intro")
            ],
            Graphs = [CreateIntroGraph()]
        };

        await _projectFiles.SaveAsync(projectRoot, project, cancellationToken).ConfigureAwait(false);
        await WriteDefaultBuildProfileAsync(projectRoot, projectName, cancellationToken).ConfigureAwait(false);
        await WriteReadmeAsync(projectRoot, projectName, cancellationToken).ConfigureAwait(false);
        return project;
    }

    public async Task<PompoProjectDocument> CreateSampleVisualNovelAsync(
        string projectRoot,
        string projectName,
        CancellationToken cancellationToken = default)
    {
        var sampleAssets = await WriteSampleAssetsAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        var project = new PompoProjectDocument
        {
            ProjectName = projectName,
            StartSceneId = "scene_intro",
            Assets = new AssetDatabase { Assets = sampleAssets },
            StringTables = [CreateDefaultStringTable()],
            Characters =
            [
                new CharacterDefinition("hero", "Hero", null, []),
                new CharacterDefinition(
                    "mina",
                    "Mina",
                    "smile",
                    [
                        new CharacterExpression("smile", new PompoAssetRef("mina-smile", PompoAssetType.Image)),
                        new CharacterExpression("surprised", new PompoAssetRef("mina-smile", PompoAssetType.Image))
                    ])
            ],
            Scenes =
            [
                new SceneDefinition(
                    "scene_intro",
                    "Intro",
                    [new SceneLayer("background", RuntimeLayer.Background, new PompoAssetRef("bg-intro", PompoAssetType.Image))],
                    [],
                    "graph_intro"),
                new SceneDefinition(
                    "scene_cafe",
                    "Cafe",
                    [new SceneLayer("background", RuntimeLayer.Background, new PompoAssetRef("bg-cafe", PompoAssetType.Image))],
                    [new SceneCharacterPlacement("mina", "mina", RuntimeLayer.Character, 0.5f, 1f, "smile")],
                    "graph_cafe"),
                new SceneDefinition(
                    "scene_rooftop",
                    "Rooftop",
                    [new SceneLayer("background", RuntimeLayer.Background, new PompoAssetRef("bg-rooftop", PompoAssetType.Image))],
                    [],
                    "graph_rooftop")
            ],
            Graphs =
            [
                CreateSampleIntroGraph(),
                CreateSampleCafeGraph(),
                CreateSampleCafeBonusGraph(),
                CreateSampleRooftopGraph()
            ]
        };

        await _projectFiles.SaveAsync(projectRoot, project, cancellationToken).ConfigureAwait(false);
        await WriteDefaultBuildProfileAsync(projectRoot, projectName, cancellationToken).ConfigureAwait(false);
        await WriteReadmeAsync(projectRoot, projectName, cancellationToken).ConfigureAwait(false);
        return project;
    }

    public static GraphDocument CreateIntroGraph()
    {
        var start = new GraphNode(
            "start",
            GraphNodeKind.Start,
            new GraphPoint(0, 0),
            [OutExecPort()],
            []);

        var narration = new GraphNode(
            "narration_opening",
            GraphNodeKind.Narration,
            new GraphPoint(240, 0),
            [InExecPort(), OutExecPort()],
            new JsonObject
            {
                ["text"] = "PompoEngine sample project is ready."
            });

        var setVariable = new GraphNode(
            "set_seen_intro",
            GraphNodeKind.SetVariable,
            new GraphPoint(520, 0),
            [InExecPort(), OutExecPort()],
            new JsonObject
            {
                ["name"] = "seenIntro",
                ["valueType"] = "bool",
                ["value"] = true
            });

        var end = new GraphNode(
            "end_scene",
            GraphNodeKind.EndScene,
            new GraphPoint(800, 0),
            [InExecPort()],
            []);

        return new GraphDocument(
            ProjectConstants.CurrentSchemaVersion,
            "graph_intro",
            [start, narration, setVariable, end],
            [
                new GraphEdge("edge_start_opening", "start", "out", "narration_opening", "in"),
                new GraphEdge("edge_opening_variable", "narration_opening", "out", "set_seen_intro", "in"),
                new GraphEdge("edge_variable_end", "set_seen_intro", "out", "end_scene", "in")
            ]);
    }

    private static GraphPort InExecPort()
    {
        return new GraphPort("in", "in", GraphPortKind.Execution, GraphValueType.None, true);
    }

    private static GraphPort OutExecPort()
    {
        return new GraphPort("out", "out", GraphPortKind.Execution, GraphValueType.None, false);
    }

    private static GraphPort OutExecPort(string id, string name)
    {
        return new GraphPort(id, name, GraphPortKind.Execution, GraphValueType.None, false);
    }

    private static StringTableDocument CreateDefaultStringTable()
    {
        return new StringTableDocument(
            "ui",
            [
                new StringTableEntry(
                    "menu.start",
                    new Dictionary<string, string>
                    {
                        ["ko"] = "시작",
                        ["en"] = "Start"
                    }),
                new StringTableEntry(
                    "menu.load",
                    new Dictionary<string, string>
                    {
                        ["ko"] = "불러오기",
                        ["en"] = "Load"
                    }),
                new StringTableEntry(
                    "menu.save",
                    new Dictionary<string, string>
                    {
                        ["ko"] = "저장",
                        ["en"] = "Save"
                    })
            ]);
    }

    private static GraphDocument CreateSampleIntroGraph()
    {
        var start = new GraphNode("start", GraphNodeKind.Start, new GraphPoint(0, 0), [OutExecPort()], []);
        var background = new GraphNode(
            "set_intro_background",
            GraphNodeKind.ChangeBackground,
            new GraphPoint(180, 0),
            [InExecPort(), OutExecPort()],
            new JsonObject { ["assetId"] = "bg-intro" });
        var music = new GraphNode(
            "play_intro_music",
            GraphNodeKind.PlayBgm,
            new GraphPoint(360, 0),
            [InExecPort(), OutExecPort()],
            new JsonObject { ["assetId"] = "music-theme" });
        var voice = new GraphNode(
            "play_opening_voice",
            GraphNodeKind.PlayVoice,
            new GraphPoint(520, -110),
            [InExecPort(), OutExecPort()],
            new JsonObject { ["assetId"] = "voice-opening" });
        var opening = new GraphNode(
            "opening",
            GraphNodeKind.Narration,
            new GraphPoint(560, 0),
            [InExecPort(), OutExecPort()],
            new JsonObject { ["text"] = "A small bell rings as the story begins." });
        var chime = new GraphNode(
            "play_choice_chime",
            GraphNodeKind.PlaySfx,
            new GraphPoint(760, 0),
            [InExecPort(), OutExecPort()],
            new JsonObject { ["assetId"] = "sfx-chime" });
        var stopVoice = new GraphNode(
            "stop_opening_voice",
            GraphNodeKind.StopVoice,
            new GraphPoint(850, -110),
            [InExecPort(), OutExecPort()],
            []);
        var choice = new GraphNode(
            "first_choice",
            GraphNodeKind.Choice,
            new GraphPoint(960, 0),
            [InExecPort(), OutExecPort("choice", "choice"), OutExecPort("cafe", "cafe"), OutExecPort("rooftop", "rooftop")],
            new JsonObject
            {
                ["choices"] = new JsonArray
                {
                    new JsonObject { ["text"] = "Visit the cafe", ["port"] = "cafe" },
                    new JsonObject { ["text"] = "Go to the rooftop", ["port"] = "rooftop" }
                }
            });
        var cafeRoute = new GraphNode(
            "set_route_cafe",
            GraphNodeKind.SetVariable,
            new GraphPoint(1240, -80),
            [InExecPort(), OutExecPort()],
            new JsonObject { ["name"] = "route", ["valueType"] = "string", ["value"] = "cafe" });
        var rooftopRoute = new GraphNode(
            "set_route_rooftop",
            GraphNodeKind.SetVariable,
            new GraphPoint(1240, 80),
            [InExecPort(), OutExecPort()],
            new JsonObject { ["name"] = "route", ["valueType"] = "string", ["value"] = "rooftop" });
        var savePoint = new GraphNode(
            "save_after_choice",
            GraphNodeKind.SavePoint,
            new GraphPoint(1520, 0),
            [InExecPort(), OutExecPort()],
            new JsonObject { ["slotHint"] = "auto_intro_choice" });
        var stopMusic = new GraphNode(
            "stop_intro_music",
            GraphNodeKind.StopBgm,
            new GraphPoint(1740, 0),
            [InExecPort(), OutExecPort()],
            []);
        var end = new GraphNode("end_intro", GraphNodeKind.EndScene, new GraphPoint(1960, 0), [InExecPort()], []);

        return new GraphDocument(
            ProjectConstants.CurrentSchemaVersion,
            "graph_intro",
            [start, background, music, voice, opening, chime, stopVoice, choice, cafeRoute, rooftopRoute, savePoint, stopMusic, end],
            [
                new GraphEdge("e_start_background", "start", "out", "set_intro_background", "in"),
                new GraphEdge("e_background_music", "set_intro_background", "out", "play_intro_music", "in"),
                new GraphEdge("e_music_voice", "play_intro_music", "out", "play_opening_voice", "in"),
                new GraphEdge("e_voice_opening", "play_opening_voice", "out", "opening", "in"),
                new GraphEdge("e_opening_chime", "opening", "out", "play_choice_chime", "in"),
                new GraphEdge("e_chime_stop_voice", "play_choice_chime", "out", "stop_opening_voice", "in"),
                new GraphEdge("e_stop_voice_choice", "stop_opening_voice", "out", "first_choice", "in"),
                new GraphEdge("e_choice_cafe", "first_choice", "cafe", "set_route_cafe", "in"),
                new GraphEdge("e_choice_rooftop", "first_choice", "rooftop", "set_route_rooftop", "in"),
                new GraphEdge("e_cafe_save", "set_route_cafe", "out", "save_after_choice", "in"),
                new GraphEdge("e_rooftop_save", "set_route_rooftop", "out", "save_after_choice", "in"),
                new GraphEdge("e_save_stop_music", "save_after_choice", "out", "stop_intro_music", "in"),
                new GraphEdge("e_stop_music_end", "stop_intro_music", "out", "end_intro", "in")
            ]);
    }

    private static GraphDocument CreateSampleCafeGraph()
    {
        var start = new GraphNode("start", GraphNodeKind.Start, new GraphPoint(0, 0), [OutExecPort()], []);
        var background = new GraphNode(
            "set_cafe_background",
            GraphNodeKind.ChangeBackground,
            new GraphPoint(200, 0),
            [InExecPort(), OutExecPort()],
            new JsonObject { ["assetId"] = "bg-cafe" });
        var showMina = new GraphNode(
            "show_mina",
            GraphNodeKind.ShowCharacter,
            new GraphPoint(460, 0),
            [InExecPort(), OutExecPort()],
            new JsonObject
            {
                ["characterId"] = "mina",
                ["expressionId"] = "smile",
                ["layer"] = "Character",
                ["x"] = 0.5f,
                ["y"] = 1f
            });
        var mina = new GraphNode(
            "mina_line",
            GraphNodeKind.Dialogue,
            new GraphPoint(720, 0),
            [InExecPort(), OutExecPort()],
            new JsonObject { ["speaker"] = "Mina", ["text"] = "The cafe route is now ready for editing." });
        var moveMina = new GraphNode(
            "move_mina_forward",
            GraphNodeKind.MoveCharacter,
            new GraphPoint(980, -80),
            [InExecPort(), OutExecPort()],
            new JsonObject
            {
                ["characterId"] = "mina",
                ["layer"] = "CharacterFront",
                ["x"] = 0.62f,
                ["y"] = 0.95f
            });
        var changeExpression = new GraphNode(
            "mina_surprised",
            GraphNodeKind.ChangeExpression,
            new GraphPoint(1220, -80),
            [InExecPort(), OutExecPort()],
            new JsonObject { ["characterId"] = "mina", ["expressionId"] = "surprised" });
        var unlockCg = new GraphNode(
            "unlock_cafe_cg",
            GraphNodeKind.UnlockCg,
            new GraphPoint(1460, -80),
            [InExecPort(), OutExecPort()],
            new JsonObject { ["cgId"] = "cg-cafe-meeting" });
        var callBonus = new GraphNode(
            "call_cafe_bonus",
            GraphNodeKind.CallGraph,
            new GraphPoint(1700, -80),
            [InExecPort(), OutExecPort()],
            new JsonObject { ["graphId"] = "graph_cafe_bonus" });
        var setFlag = new GraphNode(
            "set_met_mina",
            GraphNodeKind.SetVariable,
            new GraphPoint(1940, -80),
            [InExecPort(), OutExecPort()],
            new JsonObject { ["name"] = "metMina", ["valueType"] = "bool", ["value"] = true });
        var hideMina = new GraphNode(
            "hide_mina",
            GraphNodeKind.HideCharacter,
            new GraphPoint(2180, -80),
            [InExecPort(), OutExecPort()],
            new JsonObject { ["characterId"] = "mina" });
        var end = new GraphNode("end_cafe", GraphNodeKind.EndScene, new GraphPoint(2420, -80), [InExecPort()], []);

        return new GraphDocument(
            ProjectConstants.CurrentSchemaVersion,
            "graph_cafe",
            [start, background, showMina, mina, moveMina, changeExpression, unlockCg, callBonus, setFlag, hideMina, end],
            [
                new GraphEdge("e_start_background", "start", "out", "set_cafe_background", "in"),
                new GraphEdge("e_background_show_mina", "set_cafe_background", "out", "show_mina", "in"),
                new GraphEdge("e_show_mina_line", "show_mina", "out", "mina_line", "in"),
                new GraphEdge("e_mina_move", "mina_line", "out", "move_mina_forward", "in"),
                new GraphEdge("e_move_expression", "move_mina_forward", "out", "mina_surprised", "in"),
                new GraphEdge("e_expression_unlock", "mina_surprised", "out", "unlock_cafe_cg", "in"),
                new GraphEdge("e_unlock_bonus", "unlock_cafe_cg", "out", "call_cafe_bonus", "in"),
                new GraphEdge("e_bonus_flag", "call_cafe_bonus", "out", "set_met_mina", "in"),
                new GraphEdge("e_flag_hide", "set_met_mina", "out", "hide_mina", "in"),
                new GraphEdge("e_hide_end", "hide_mina", "out", "end_cafe", "in")
            ]);
    }

    private static GraphDocument CreateSampleCafeBonusGraph()
    {
        var start = new GraphNode("start", GraphNodeKind.Start, new GraphPoint(0, 0), [OutExecPort()], []);
        var line = new GraphNode(
            "bonus_line",
            GraphNodeKind.Narration,
            new GraphPoint(220, 0),
            [InExecPort(), OutExecPort()],
            new JsonObject { ["text"] = "A short reusable cafe beat returns to the caller." });
        var ret = new GraphNode("return_to_cafe", GraphNodeKind.Return, new GraphPoint(520, 0), [InExecPort()], []);

        return new GraphDocument(
            ProjectConstants.CurrentSchemaVersion,
            "graph_cafe_bonus",
            [start, line, ret],
            [
                new GraphEdge("e_start_line", "start", "out", "bonus_line", "in"),
                new GraphEdge("e_line_return", "bonus_line", "out", "return_to_cafe", "in")
            ]);
    }

    private static GraphDocument CreateSampleRooftopGraph()
    {
        var start = new GraphNode("start", GraphNodeKind.Start, new GraphPoint(0, 0), [OutExecPort()], []);
        var background = new GraphNode(
            "set_rooftop_background",
            GraphNodeKind.ChangeBackground,
            new GraphPoint(200, 0),
            [InExecPort(), OutExecPort()],
            new JsonObject { ["assetId"] = "bg-rooftop" });
        var branch = new GraphNode(
            "branch_met_mina",
            GraphNodeKind.Branch,
            new GraphPoint(460, 0),
            [InExecPort(), OutExecPort("true", "true"), OutExecPort("false", "false"), new GraphPort("condition", "condition", GraphPortKind.Data, GraphValueType.Bool, true)],
            new JsonObject { ["variable"] = "metMina" });
        var trueLine = new GraphNode(
            "true_line",
            GraphNodeKind.Narration,
            new GraphPoint(520, -80),
            [InExecPort(), OutExecPort()],
            new JsonObject { ["text"] = "The rooftop feels warmer after meeting Mina." });
        var falseLine = new GraphNode(
            "false_line",
            GraphNodeKind.Narration,
            new GraphPoint(520, 80),
            [InExecPort(), OutExecPort()],
            new JsonObject { ["text"] = "The rooftop is quiet and unfamiliar." });
        var end = new GraphNode("end_rooftop", GraphNodeKind.EndScene, new GraphPoint(820, 0), [InExecPort()], []);

        return new GraphDocument(
            ProjectConstants.CurrentSchemaVersion,
            "graph_rooftop",
            [start, background, branch, trueLine, falseLine, end],
            [
                new GraphEdge("e_start_background", "start", "out", "set_rooftop_background", "in"),
                new GraphEdge("e_background_branch", "set_rooftop_background", "out", "branch_met_mina", "in"),
                new GraphEdge("e_branch_true", "branch_met_mina", "true", "true_line", "in"),
                new GraphEdge("e_branch_false", "branch_met_mina", "false", "false_line", "in"),
                new GraphEdge("e_true_end", "true_line", "out", "end_rooftop", "in"),
                new GraphEdge("e_false_end", "false_line", "out", "end_rooftop", "in")
            ]);
    }

    private static async Task<List<AssetMetadata>> WriteSampleAssetsAsync(
        string projectRoot,
        CancellationToken cancellationToken)
    {
        var assets = new List<AssetMetadata>();
        foreach (var asset in new[]
        {
            ("bg-intro", "Assets/Images/bg-intro.png"),
            ("bg-cafe", "Assets/Images/bg-cafe.png"),
            ("bg-rooftop", "Assets/Images/bg-rooftop.png"),
            ("mina-smile", "Assets/Images/mina-smile.png")
        })
        {
            var path = Path.Combine(projectRoot, asset.Item2);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, SamplePngBytes, cancellationToken).ConfigureAwait(false);
            assets.Add(new AssetMetadata(
                asset.Item1,
                asset.Item2,
                PompoAssetType.Image,
                new AssetImportOptions(),
                await AssetDatabaseService.ComputeSha256Async(path, cancellationToken).ConfigureAwait(false),
                []));
        }

        foreach (var asset in new[]
        {
            ("music-theme", "Assets/Audio/music-theme.wav", true),
            ("sfx-chime", "Assets/Audio/sfx-chime.wav", false),
            ("voice-opening", "Assets/Audio/voice-opening.wav", false)
        })
        {
            var path = Path.Combine(projectRoot, asset.Item2);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, CreateSilentWavBytes(), cancellationToken).ConfigureAwait(false);
            assets.Add(new AssetMetadata(
                asset.Item1,
                asset.Item2,
                PompoAssetType.Audio,
                new AssetImportOptions(Loop: asset.Item3),
                await AssetDatabaseService.ComputeSha256Async(path, cancellationToken).ConfigureAwait(false),
                []));
        }

        return assets;
    }

    private static byte[] SamplePngBytes => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");

    private static byte[] CreateSilentWavBytes()
    {
        const int sampleRate = 8000;
        const short channelCount = 1;
        const short bitsPerSample = 16;
        const int sampleCount = sampleRate / 4;
        var dataSize = sampleCount * channelCount * (bitsPerSample / 8);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channelCount);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channelCount * (bitsPerSample / 8));
        writer.Write((short)(channelCount * (bitsPerSample / 8)));
        writer.Write(bitsPerSample);
        writer.Write("data"u8);
        writer.Write(dataSize);
        for (var index = 0; index < sampleCount; index++)
        {
            writer.Write((short)0);
        }

        return stream.ToArray();
    }

    private static async Task WriteReadmeAsync(
        string projectRoot,
        string projectName,
        CancellationToken cancellationToken)
    {
        var readme = $"""
            # {projectName}

            This is a PompoEngine visual novel project.

            Open `project.pompo.json` through the Pompo editor, then use
            Workspace, Theme, Preview, and Build tabs to author and package the
            project.

            Run project health checks before packaging:

            ```bash
            pompo doctor --project .
            pompo validate --project . --json
            pompo asset verify --project . --json
            pompo localization report --project . --json
            ```

            Build and verify standalone runtime output:

            ```bash
            pompo build --project . --output Builds --platform MacOS
            pompo build --project . --profile-file BuildProfiles/release.pompo-build.json --output Builds
            pompo build verify --build Builds/MacOS/release --require-smoke-tested-locales --require-self-contained --json
            ```

            Package and verify a release candidate:

            ```bash
            pompo release package --build Builds/MacOS/release --output Releases --name {projectName}-0.1.0-macos --json
            pompo release verify --manifest Releases/{projectName}-0.1.0-macos.release.json --require-smoke-tested-locales --require-self-contained --json
            pompo release audit --root <PompoEngineRepositoryRoot> --manifest Releases/{projectName}-0.1.0-macos.release.json --require-smoke-tested-locales --require-self-contained --json
            ```

            The release profile runs packaged-runtime smoke tests and expects a
            self-contained runtime. Debug builds are useful for local iteration;
            release builds are the supported distribution path.
            """;

        await AtomicFileWriter.WriteTextAsync(Path.Combine(projectRoot, "README.md"), readme, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task WriteDefaultBuildProfileAsync(
        string projectRoot,
        string projectName,
        CancellationToken cancellationToken)
    {
        var profileDirectory = Path.Combine(projectRoot, "BuildProfiles");
        Directory.CreateDirectory(profileDirectory);

        await WriteBuildProfileAsync(
                Path.Combine(profileDirectory, "debug.pompo-build.json"),
                projectName,
                "debug",
                runSmokeTest: false,
                selfContained: false,
                cancellationToken)
            .ConfigureAwait(false);
        await WriteBuildProfileAsync(
                Path.Combine(profileDirectory, "release.pompo-build.json"),
                projectName,
                "release",
                runSmokeTest: true,
                selfContained: true,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task WriteBuildProfileAsync(
        string profilePath,
        string projectName,
        string profileName,
        bool runSmokeTest,
        bool selfContained,
        CancellationToken cancellationToken)
    {
        var profile = new
        {
            profileName,
            platform = "MacOS",
            appName = projectName,
            version = "0.1.0",
            iconPath = (string?)null,
            runSmokeTest,
            packageRuntime = true,
            runtimeProjectPath = (string?)null,
            selfContained
        };
        await AtomicFileWriter.WriteJsonAsync(
                profilePath,
                profile,
                ProjectFileService.CreateJsonOptions(),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
