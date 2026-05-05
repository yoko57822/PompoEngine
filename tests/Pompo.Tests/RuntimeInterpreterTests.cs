using System.Text.Json.Nodes;
using Pompo.Core.Localization;
using Pompo.Core.Runtime;
using Pompo.Core.Graphs;
using Pompo.VisualScripting;
using Pompo.VisualScripting.Runtime;

namespace Pompo.Tests;

public sealed class RuntimeInterpreterTests
{
    [Fact]
    public void Step_ExecutesNarrationVariableBranchAndEnd()
    {
        var ir = new GraphCompiler().Compile(CreateBranchGraph());
        var runtime = new GraphRuntimeInterpreter(ir);

        Assert.False(runtime.Step().IsComplete);
        var line = runtime.Step().CurrentLine;
        Assert.NotNull(line);
        Assert.True(line.IsNarration);
        Assert.Equal("Ready", line.Text);

        var afterVariable = runtime.Step();
        Assert.True(afterVariable.Variables["flag"] is true);

        var afterBranch = runtime.Step();
        Assert.False(afterBranch.IsComplete);
        Assert.Equal("true_line", ir.Instructions[afterBranch.InstructionPointer].SourceNodeId);

        var trueLine = runtime.Step();
        Assert.Equal("True path", trueLine.CurrentLine?.Text);

        Assert.True(runtime.Step().IsComplete);
    }

    [Fact]
    public void FromSaveData_RestoresInstructionPointerAndVariables()
    {
        var ir = new GraphCompiler().Compile(CreateBranchGraph());
        var runtime = new GraphRuntimeInterpreter(ir);

        runtime.Step();
        runtime.Step();
        var afterVariable = runtime.Step();
        Assert.True(afterVariable.Variables["flag"] is true);

        var save = runtime.CreateSaveData();
        var restored = GraphRuntimeInterpreter.FromSaveData(ir, save);

        Assert.Equal(save.NodeId, ir.Instructions[restored.Snapshot.InstructionPointer].SourceNodeId);
        Assert.True(restored.Snapshot.Variables["flag"] is true);
        Assert.Equal("true_line", ir.Instructions[restored.Step().InstructionPointer].SourceNodeId);
    }

    [Fact]
    public void RuntimeTraceRunner_RecordsChoiceSelectionVariablesAndCompletion()
    {
        var ir = new GraphCompiler().Compile(CreateChoiceGraph());

        var result = new RuntimeTraceRunner().Run(ir, [1]);

        Assert.True(result.Completed);
        Assert.Contains(result.Events, traceEvent => traceEvent.Kind == "choice" && traceEvent.SelectedChoice == "Right");
        Assert.Contains("Right", result.ChoiceHistory);
        Assert.Equal("right", result.Variables["route"]);
        Assert.NotNull(result.SaveData);
        Assert.Equal("choice", result.SaveData.GraphId);
        Assert.Equal("right", result.SaveData.Variables["route"]);
    }

    [Fact]
    public void Choice_DisabledChoiceCannotBeChosenAndTraceDefaultsToFirstEnabledChoice()
    {
        var ir = new GraphCompiler().Compile(CreateDisabledChoiceGraph());
        var runtime = new GraphRuntimeInterpreter(ir);

        runtime.Step();
        var choiceSnapshot = runtime.Step();

        Assert.Equal(2, choiceSnapshot.Choices.Count);
        Assert.False(choiceSnapshot.Choices[0].IsEnabled);
        Assert.True(choiceSnapshot.Choices[1].IsEnabled);
        Assert.Throws<InvalidOperationException>(() => runtime.Choose(0));

        var result = new RuntimeTraceRunner().Run(ir);

        Assert.True(result.Completed);
        Assert.Contains(result.Events, traceEvent => traceEvent.Kind == "choice" && traceEvent.SelectedChoice == "Open");
        Assert.Equal("open", result.Variables["route"]);
    }

    [Fact]
    public void RuntimeTraceRunner_RejectsExplicitDisabledChoiceSelection()
    {
        var ir = new GraphCompiler().Compile(CreateDisabledChoiceGraph());

        var exception = Assert.Throws<InvalidOperationException>(() => new RuntimeTraceRunner().Run(ir, [0]));

        Assert.Contains("disabled", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeTraceRunner_RejectsChoiceStopWithNoEnabledChoices()
    {
        var ir = CreateAllDisabledChoiceIr();

        var exception = Assert.Throws<InvalidOperationException>(() => new RuntimeTraceRunner().Run(ir));

        Assert.Contains("no enabled choices", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeTraceRunner_RejectsMalformedChoiceIr()
    {
        var ir = CreateMalformedChoiceIr();

        var exception = Assert.Throws<InvalidDataException>(() => new RuntimeTraceRunner().Run(ir));

        Assert.Contains("malformed choice entry", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeTraceRunner_RejectsChoiceIrWithNonStringPort()
    {
        var ir = CreateChoiceIrWithNonStringPort();

        var exception = Assert.Throws<InvalidDataException>(() => new RuntimeTraceRunner().Run(ir));

        Assert.Contains("non-string port", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeTraceRunner_JumpMovesToTargetNode()
    {
        var ir = new GraphCompiler().Compile(CreateJumpGraph());

        var result = new RuntimeTraceRunner().Run(ir);

        Assert.True(result.Completed);
        Assert.DoesNotContain(result.Events, traceEvent => traceEvent.Kind == "line" && traceEvent.Text == "Skipped");
        Assert.Contains(result.Events, traceEvent => traceEvent.Kind == "line" && traceEvent.Text == "Target");
    }

    [Fact]
    public void RuntimeTraceRunner_CallGraphExecutesTargetAndReturns()
    {
        var graphLibrary = CompileGraphLibrary(CreateCallGraphRootGraph(), CreateCallGraphChildGraph());

        var result = new RuntimeTraceRunner().Run(graphLibrary["root"], graphLibrary: graphLibrary);

        Assert.True(result.Completed);
        var lines = result.Events
            .Where(traceEvent => traceEvent.Kind == "line")
            .Select(traceEvent => $"{traceEvent.GraphId}:{traceEvent.Text}")
            .ToArray();
        Assert.Equal(["child:Inside child graph", "root:Back in root graph"], lines);
        Assert.NotNull(result.SaveData);
        Assert.Empty(result.SaveData.CallStack);
    }

    [Fact]
    public void FromSaveData_RestoresCallStackAndReturnsToCallerGraph()
    {
        var graphLibrary = CompileGraphLibrary(CreateCallGraphRootGraph(), CreateCallGraphChildGraph());
        var runtime = new GraphRuntimeInterpreter(graphLibrary["root"], graphLibrary: graphLibrary);

        runtime.Step();
        var inChild = runtime.Step();
        var save = runtime.CreateSaveData();

        Assert.Equal("child", inChild.GraphId);
        Assert.Equal(["root:2"], save.CallStack);

        var restored = GraphRuntimeInterpreter.FromSaveData(graphLibrary, save);
        restored.Step();
        var childLine = restored.Step();
        restored.Step();
        var rootLine = restored.Step();

        Assert.Equal("Inside child graph", childLine.CurrentLine?.Text);
        Assert.Equal("root", rootLine.GraphId);
        Assert.Equal("Back in root graph", rootLine.CurrentLine?.Text);
        Assert.Empty(rootLine.CallStack);
    }

    [Fact]
    public void RuntimeTraceRunner_RecordsBackgroundAndCharacterState()
    {
        var ir = new GraphCompiler().Compile(CreateVisualStateGraph());

        var result = new RuntimeTraceRunner().Run(ir);

        Assert.True(result.Completed);
        Assert.Equal("bg-cafe", result.BackgroundAssetId);
        var character = Assert.Single(result.Characters);
        Assert.Equal("mina", character.CharacterId);
        Assert.Equal("smile", character.ExpressionId);
        Assert.Equal(RuntimeLayer.CharacterFront, character.Layer);
        Assert.True(character.Visible);
        Assert.Equal(0.35f, character.X);
    }

    [Fact]
    public void RuntimeTraceRunner_RecordsCharacterMovementExpressionHideAndUnlockedCg()
    {
        var ir = new GraphCompiler().Compile(CreateCharacterMovementAndCgGraph());

        var result = new RuntimeTraceRunner().Run(ir);

        Assert.True(result.Completed);
        var character = Assert.Single(result.Characters);
        Assert.Equal("mina", character.CharacterId);
        Assert.Equal("surprised", character.ExpressionId);
        Assert.Equal(RuntimeLayer.CharacterFront, character.Layer);
        Assert.Equal(0.72f, character.X);
        Assert.Equal(0.88f, character.Y);
        Assert.False(character.Visible);
        Assert.Equal(["cg-ending"], result.UnlockedCgIds);
        Assert.NotNull(result.SaveData);
        Assert.Equal(["cg-ending"], result.SaveData.UnlockedCgIds);

        var restored = GraphRuntimeInterpreter.FromSaveData(ir, result.SaveData);
        Assert.Equal(["cg-ending"], restored.Snapshot.UnlockedCgIds);
        Assert.Equal(0.72f, restored.Snapshot.Characters.Single().X);
    }

    [Fact]
    public void RuntimeTraceRunner_RecordsAudioStateAndSaveRestoresIt()
    {
        var ir = new GraphCompiler().Compile(CreateAudioGraph());
        var runtime = new GraphRuntimeInterpreter(ir);

        runtime.Step();
        runtime.Step();
        runtime.Step();
        var afterVoice = runtime.Step();
        var save = runtime.CreateSaveData();
        var result = new RuntimeTraceRunner().Run(ir);

        Assert.Equal("bgm-main", afterVoice.Audio.BgmAssetId);
        Assert.Contains("sfx-chime", afterVoice.Audio.PlayingSfxAssetIds);
        Assert.Equal("voice-line", afterVoice.Audio.VoiceAssetId);
        Assert.Null(result.Audio.BgmAssetId);
        Assert.Null(result.Audio.VoiceAssetId);
        Assert.Contains("sfx-chime", result.Audio.PlayingSfxAssetIds);
        Assert.Equal("bgm-main", save.Audio.BgmAssetId);
        Assert.Equal("voice-line", save.Audio.VoiceAssetId);
        Assert.Contains("sfx-chime", save.Audio.PlayingSfxAssetIds);

        var restored = GraphRuntimeInterpreter.FromSaveData(ir, save);
        Assert.Equal("bgm-main", restored.Snapshot.Audio.BgmAssetId);
        Assert.Equal("voice-line", restored.Snapshot.Audio.VoiceAssetId);
        Assert.Contains("sfx-chime", restored.Snapshot.Audio.PlayingSfxAssetIds);
    }

    [Fact]
    public void RuntimeTraceRunner_ResolvesLocalizedDialogueSpeakerAndChoices()
    {
        var ir = new GraphCompiler().Compile(CreateLocalizedGraph());
        var localizer = CreateKoreanLocalizer();

        var result = new RuntimeTraceRunner().Run(ir, [1], localizer: localizer);

        Assert.Contains(result.Events, traceEvent =>
            traceEvent.Kind == "line" &&
            traceEvent.Speaker == "미나" &&
            traceEvent.Text == "어서 와.");
        Assert.Contains(result.Events, traceEvent =>
            traceEvent.Kind == "choice" &&
            traceEvent.Choices is not null &&
            traceEvent.Choices.SequenceEqual(["왼쪽", "오른쪽"]) &&
            traceEvent.SelectedChoice == "오른쪽");
        Assert.Contains("오른쪽", result.ChoiceHistory);
    }

    [Fact]
    public void FromSaveData_ResolvesLocalizedLineAfterRestore()
    {
        var ir = new GraphCompiler().Compile(CreateLocalizedGraph());
        var save = new RuntimeSaveData(
            1,
            "localized",
            "line",
            [],
            new Dictionary<string, object?>(),
            null,
            [],
            new RuntimeAudioState(null, []),
            []);

        var restored = GraphRuntimeInterpreter.FromSaveData(ir, save, CreateKoreanLocalizer());
        var snapshot = restored.Step();

        Assert.Equal("미나", snapshot.CurrentLine?.Speaker);
        Assert.Equal("어서 와.", snapshot.CurrentLine?.Text);
    }

    [Fact]
    public void RuntimeTraceRunner_ExecutesCustomNodeHandler()
    {
        var ir = new GraphCompiler().Compile(CreateCustomCommandGraph());

        var result = new RuntimeTraceRunner().Run(
            ir,
            customNodeHandler: new TestCustomNodeHandler());

        Assert.True(result.Completed);
        Assert.Equal("custom-value", result.Variables["custom"]);
        Assert.Contains(result.Events, traceEvent => traceEvent.Kind == "line" && traceEvent.Text == "Custom node ran.");
    }

    [Fact]
    public void RuntimeTraceRunner_UsesCustomConditionOutputPort()
    {
        var ir = new GraphCompiler().Compile(CreateCustomConditionGraph());

        var result = new RuntimeTraceRunner().Run(
            ir,
            customNodeHandler: new TestCustomNodeHandler());

        Assert.True(result.Completed);
        Assert.DoesNotContain(result.Events, traceEvent => traceEvent.Kind == "line" && traceEvent.Text == "False path");
        Assert.Contains(result.Events, traceEvent => traceEvent.Kind == "line" && traceEvent.Text == "True path");
    }

    private static GraphDocument CreateBranchGraph()
    {
        var start = Node("start", GraphNodeKind.Start, 0, [NodeCatalog.OutExecPort()], []);
        var narration = Node(
            "line",
            GraphNodeKind.Narration,
            1,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["text"] = "Ready" });
        var set = Node(
            "set",
            GraphNodeKind.SetVariable,
            2,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["name"] = "flag", ["value"] = true });
        var branch = Node(
            "branch",
            GraphNodeKind.Branch,
            3,
            [
                NodeCatalog.InExecPort(),
                NodeCatalog.OutExecPort("true", "true"),
                NodeCatalog.OutExecPort("false", "false"),
                new GraphPort("condition", "condition", GraphPortKind.Data, GraphValueType.Bool, true)
            ],
            new JsonObject { ["variable"] = "flag" });
        var trueLine = Node(
            "true_line",
            GraphNodeKind.Narration,
            4,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["text"] = "True path" });
        var falseLine = Node(
            "false_line",
            GraphNodeKind.Narration,
            5,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["text"] = "False path" });
        var end = Node("end", GraphNodeKind.EndScene, 6, [NodeCatalog.InExecPort()], []);

        return new GraphDocument(
            1,
            "branch",
            [start, narration, set, branch, trueLine, falseLine, end],
            [
                new GraphEdge("e1", "start", "out", "line", "in"),
                new GraphEdge("e2", "line", "out", "set", "in"),
                new GraphEdge("e3", "set", "out", "branch", "in"),
                new GraphEdge("e4", "branch", "true", "true_line", "in"),
                new GraphEdge("e5", "branch", "false", "false_line", "in"),
                new GraphEdge("e6", "true_line", "out", "end", "in"),
                new GraphEdge("e7", "false_line", "out", "end", "in")
            ]);
    }

    private static GraphDocument CreateChoiceGraph()
    {
        var start = Node("start", GraphNodeKind.Start, 0, [NodeCatalog.OutExecPort()], []);
        var choice = Node(
            "choice",
            GraphNodeKind.Choice,
            1,
            [
                NodeCatalog.InExecPort(),
                NodeCatalog.OutExecPort("choice", "choice"),
                NodeCatalog.OutExecPort("left", "left"),
                NodeCatalog.OutExecPort("right", "right")
            ],
            new JsonObject
            {
                ["choices"] = new JsonArray
                {
                    new JsonObject { ["text"] = "Left", ["port"] = "left" },
                    new JsonObject { ["text"] = "Right", ["port"] = "right" }
                }
            });
        var left = Node(
            "left",
            GraphNodeKind.SetVariable,
            2,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["name"] = "route", ["value"] = "left" });
        var right = Node(
            "right",
            GraphNodeKind.SetVariable,
            3,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["name"] = "route", ["value"] = "right" });
        var end = Node("end", GraphNodeKind.EndScene, 4, [NodeCatalog.InExecPort()], []);

        return new GraphDocument(
            1,
            "choice",
            [start, choice, left, right, end],
            [
                new GraphEdge("e1", "start", "out", "choice", "in"),
                new GraphEdge("e2", "choice", "left", "left", "in"),
                new GraphEdge("e3", "choice", "right", "right", "in"),
                new GraphEdge("e4", "left", "out", "end", "in"),
                new GraphEdge("e5", "right", "out", "end", "in")
            ]);
    }

    private static GraphDocument CreateJumpGraph()
    {
        var start = Node("start", GraphNodeKind.Start, 0, [NodeCatalog.OutExecPort()], []);
        var jump = Node(
            "jump",
            GraphNodeKind.Jump,
            1,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["targetNodeId"] = "target" });
        var skipped = Node(
            "skipped",
            GraphNodeKind.Narration,
            2,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["text"] = "Skipped" });
        var target = Node(
            "target",
            GraphNodeKind.Narration,
            3,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["text"] = "Target" });
        var end = Node("end", GraphNodeKind.EndScene, 4, [NodeCatalog.InExecPort()], []);

        return new GraphDocument(
            1,
            "jump",
            [start, jump, skipped, target, end],
            [
                new GraphEdge("e1", "start", "out", "jump", "in"),
                new GraphEdge("e2", "jump", "out", "skipped", "in"),
                new GraphEdge("e3", "skipped", "out", "end", "in"),
                new GraphEdge("e4", "target", "out", "end", "in")
            ]);
    }

    private static GraphDocument CreateDisabledChoiceGraph()
    {
        var start = Node("start", GraphNodeKind.Start, 0, [NodeCatalog.OutExecPort()], []);
        var choice = Node(
            "choice",
            GraphNodeKind.Choice,
            1,
            [
                NodeCatalog.InExecPort(),
                NodeCatalog.OutExecPort("choice", "choice"),
                NodeCatalog.OutExecPort("locked", "locked"),
                NodeCatalog.OutExecPort("open", "open")
            ],
            new JsonObject
            {
                ["choices"] = new JsonArray
                {
                    new JsonObject { ["text"] = "Locked", ["port"] = "locked", ["enabled"] = false },
                    new JsonObject { ["text"] = "Open", ["port"] = "open", ["enabled"] = true }
                }
            });
        var locked = Node(
            "locked",
            GraphNodeKind.SetVariable,
            2,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["name"] = "route", ["value"] = "locked" });
        var open = Node(
            "open",
            GraphNodeKind.SetVariable,
            3,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["name"] = "route", ["value"] = "open" });
        var end = Node("end", GraphNodeKind.EndScene, 4, [NodeCatalog.InExecPort()], []);

        return new GraphDocument(
            1,
            "disabled-choice",
            [start, choice, locked, open, end],
            [
                new GraphEdge("e1", "start", "out", "choice", "in"),
                new GraphEdge("e2", "choice", "locked", "locked", "in"),
                new GraphEdge("e3", "choice", "open", "open", "in"),
                new GraphEdge("e4", "locked", "out", "end", "in"),
                new GraphEdge("e5", "open", "out", "end", "in")
            ]);
    }

    private static PompoGraphIR CreateAllDisabledChoiceIr()
    {
        return new PompoGraphIR(
            "all-disabled-choice",
            [
                new PompoIRInstruction(
                    0,
                    "start",
                    GraphNodeKind.Start,
                    new Dictionary<string, JsonNode?>(),
                    new Dictionary<string, int> { ["out"] = 1 }),
                new PompoIRInstruction(
                    1,
                    "choice",
                    GraphNodeKind.Choice,
                    new Dictionary<string, JsonNode?>
                    {
                        ["choices"] = new JsonArray
                        {
                            new JsonObject { ["text"] = "Locked", ["port"] = "locked", ["enabled"] = false },
                            new JsonObject { ["text"] = "Also locked", ["port"] = "also_locked", ["enabled"] = false }
                        }
                    },
                    new Dictionary<string, int>
                    {
                        ["locked"] = 2,
                        ["also_locked"] = 2
                    }),
                new PompoIRInstruction(
                    2,
                    "end",
                    GraphNodeKind.EndScene,
                    new Dictionary<string, JsonNode?>(),
                    new Dictionary<string, int>())
            ]);
    }

    private static PompoGraphIR CreateMalformedChoiceIr()
    {
        return new PompoGraphIR(
            "malformed-choice",
            [
                new PompoIRInstruction(
                    0,
                    "start",
                    GraphNodeKind.Start,
                    new Dictionary<string, JsonNode?>(),
                    new Dictionary<string, int> { ["out"] = 1 }),
                new PompoIRInstruction(
                    1,
                    "choice",
                    GraphNodeKind.Choice,
                    new Dictionary<string, JsonNode?>
                    {
                        ["choices"] = new JsonArray { "not-an-object" }
                    },
                    new Dictionary<string, int> { ["choice"] = 2 }),
                new PompoIRInstruction(
                    2,
                    "end",
                    GraphNodeKind.EndScene,
                    new Dictionary<string, JsonNode?>(),
                    new Dictionary<string, int>())
            ]);
    }

    private static PompoGraphIR CreateChoiceIrWithNonStringPort()
    {
        return new PompoGraphIR(
            "bad-choice-port",
            [
                new PompoIRInstruction(
                    0,
                    "start",
                    GraphNodeKind.Start,
                    new Dictionary<string, JsonNode?>(),
                    new Dictionary<string, int> { ["out"] = 1 }),
                new PompoIRInstruction(
                    1,
                    "choice",
                    GraphNodeKind.Choice,
                    new Dictionary<string, JsonNode?>
                    {
                        ["choices"] = new JsonArray
                        {
                            new JsonObject { ["text"] = "Bad", ["port"] = 42 }
                        }
                    },
                    new Dictionary<string, int> { ["choice"] = 2 }),
                new PompoIRInstruction(
                    2,
                    "end",
                    GraphNodeKind.EndScene,
                    new Dictionary<string, JsonNode?>(),
                    new Dictionary<string, int>())
            ]);
    }

    private static GraphDocument CreateCallGraphRootGraph()
    {
        var start = Node("start", GraphNodeKind.Start, 0, [NodeCatalog.OutExecPort()], []);
        var call = Node(
            "call_child",
            GraphNodeKind.CallGraph,
            1,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["graphId"] = "child" });
        var after = Node(
            "after",
            GraphNodeKind.Narration,
            2,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["text"] = "Back in root graph" });
        var end = Node("end", GraphNodeKind.EndScene, 3, [NodeCatalog.InExecPort()], []);

        return new GraphDocument(
            1,
            "root",
            [start, call, after, end],
            [
                new GraphEdge("e1", "start", "out", "call_child", "in"),
                new GraphEdge("e2", "call_child", "out", "after", "in"),
                new GraphEdge("e3", "after", "out", "end", "in")
            ]);
    }

    private static GraphDocument CreateCallGraphChildGraph()
    {
        var start = Node("start", GraphNodeKind.Start, 0, [NodeCatalog.OutExecPort()], []);
        var line = Node(
            "line",
            GraphNodeKind.Narration,
            1,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["text"] = "Inside child graph" });
        var ret = Node("return", GraphNodeKind.Return, 2, [NodeCatalog.InExecPort()], []);

        return new GraphDocument(
            1,
            "child",
            [start, line, ret],
            [
                new GraphEdge("e1", "start", "out", "line", "in"),
                new GraphEdge("e2", "line", "out", "return", "in")
            ]);
    }

    private static GraphDocument CreateVisualStateGraph()
    {
        var start = Node("start", GraphNodeKind.Start, 0, [NodeCatalog.OutExecPort()], []);
        var background = Node(
            "bg",
            GraphNodeKind.ChangeBackground,
            1,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["assetId"] = "bg-cafe" });
        var show = Node(
            "show",
            GraphNodeKind.ShowCharacter,
            2,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject
            {
                ["characterId"] = "mina",
                ["expressionId"] = "smile",
                ["layer"] = "CharacterFront",
                ["x"] = 0.35f,
                ["y"] = 1f
            });
        var line = Node(
            "line",
            GraphNodeKind.Narration,
            3,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["text"] = "Mina appears." });
        var end = Node("end", GraphNodeKind.EndScene, 4, [NodeCatalog.InExecPort()], []);

        return new GraphDocument(
            1,
            "visuals",
            [start, background, show, line, end],
            [
                new GraphEdge("e1", "start", "out", "bg", "in"),
                new GraphEdge("e2", "bg", "out", "show", "in"),
                new GraphEdge("e3", "show", "out", "line", "in"),
                new GraphEdge("e4", "line", "out", "end", "in")
            ]);
    }

    private static GraphDocument CreateAudioGraph()
    {
        var start = Node("start", GraphNodeKind.Start, 0, [NodeCatalog.OutExecPort()], []);
        var bgm = Node(
            "bgm",
            GraphNodeKind.PlayBgm,
            1,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["assetId"] = "bgm-main" });
        var sfx = Node(
            "sfx",
            GraphNodeKind.PlaySfx,
            2,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["assetId"] = "sfx-chime" });
        var voice = Node(
            "voice",
            GraphNodeKind.PlayVoice,
            3,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["assetId"] = "voice-line" });
        var stop = Node(
            "stop",
            GraphNodeKind.StopBgm,
            4,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            []);
        var stopVoice = Node(
            "stop_voice",
            GraphNodeKind.StopVoice,
            5,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            []);
        var end = Node("end", GraphNodeKind.EndScene, 6, [NodeCatalog.InExecPort()], []);

        return new GraphDocument(
            1,
            "audio",
            [start, bgm, sfx, voice, stop, stopVoice, end],
            [
                new GraphEdge("e1", "start", "out", "bgm", "in"),
                new GraphEdge("e2", "bgm", "out", "sfx", "in"),
                new GraphEdge("e3", "sfx", "out", "voice", "in"),
                new GraphEdge("e4", "voice", "out", "stop", "in"),
                new GraphEdge("e5", "stop", "out", "stop_voice", "in"),
                new GraphEdge("e6", "stop_voice", "out", "end", "in")
            ]);
    }

    private static GraphDocument CreateCharacterMovementAndCgGraph()
    {
        var start = Node("start", GraphNodeKind.Start, 0, [NodeCatalog.OutExecPort()], []);
        var show = Node(
            "show",
            GraphNodeKind.ShowCharacter,
            1,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject
            {
                ["characterId"] = "mina",
                ["expressionId"] = "smile",
                ["x"] = 0.35f,
                ["y"] = 1f
            });
        var move = Node(
            "move",
            GraphNodeKind.MoveCharacter,
            2,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject
            {
                ["characterId"] = "mina",
                ["layer"] = "CharacterFront",
                ["x"] = 0.72f,
                ["y"] = 0.88f
            });
        var expression = Node(
            "expression",
            GraphNodeKind.ChangeExpression,
            3,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["characterId"] = "mina", ["expressionId"] = "surprised" });
        var unlock = Node(
            "unlock",
            GraphNodeKind.UnlockCg,
            4,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["cgId"] = "cg-ending" });
        var hide = Node(
            "hide",
            GraphNodeKind.HideCharacter,
            5,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["characterId"] = "mina" });
        var end = Node("end", GraphNodeKind.EndScene, 6, [NodeCatalog.InExecPort()], []);

        return new GraphDocument(
            1,
            "character-state",
            [start, show, move, expression, unlock, hide, end],
            [
                new GraphEdge("e1", "start", "out", "show", "in"),
                new GraphEdge("e2", "show", "out", "move", "in"),
                new GraphEdge("e3", "move", "out", "expression", "in"),
                new GraphEdge("e4", "expression", "out", "unlock", "in"),
                new GraphEdge("e5", "unlock", "out", "hide", "in"),
                new GraphEdge("e6", "hide", "out", "end", "in")
            ]);
    }

    private static GraphDocument CreateLocalizedGraph()
    {
        var start = Node("start", GraphNodeKind.Start, 0, [NodeCatalog.OutExecPort()], []);
        var line = Node(
            "line",
            GraphNodeKind.Dialogue,
            1,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject
            {
                ["speakerKey"] = "speaker.mina",
                ["speakerTableId"] = "dialogue",
                ["textKey"] = "line.greeting",
                ["tableId"] = "dialogue",
                ["speaker"] = "Mina",
                ["text"] = "Welcome."
            });
        var choice = Node(
            "choice",
            GraphNodeKind.Choice,
            2,
            [
                NodeCatalog.InExecPort(),
                NodeCatalog.OutExecPort("choice", "choice"),
                NodeCatalog.OutExecPort("left", "left"),
                NodeCatalog.OutExecPort("right", "right")
            ],
            new JsonObject
            {
                ["tableId"] = "dialogue",
                ["choices"] = new JsonArray
                {
                    new JsonObject { ["textKey"] = "choice.left", ["text"] = "Left", ["port"] = "left" },
                    new JsonObject { ["textKey"] = "choice.right", ["text"] = "Right", ["port"] = "right" }
                }
            });
        var left = Node(
            "left",
            GraphNodeKind.SetVariable,
            3,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["name"] = "route", ["value"] = "left" });
        var right = Node(
            "right",
            GraphNodeKind.SetVariable,
            4,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["name"] = "route", ["value"] = "right" });
        var end = Node("end", GraphNodeKind.EndScene, 5, [NodeCatalog.InExecPort()], []);

        return new GraphDocument(
            1,
            "localized",
            [start, line, choice, left, right, end],
            [
                new GraphEdge("e1", "start", "out", "line", "in"),
                new GraphEdge("e2", "line", "out", "choice", "in"),
                new GraphEdge("e3", "choice", "left", "left", "in"),
                new GraphEdge("e4", "choice", "right", "right", "in"),
                new GraphEdge("e5", "left", "out", "end", "in"),
                new GraphEdge("e6", "right", "out", "end", "in")
            ]);
    }

    private static GraphDocument CreateCustomCommandGraph()
    {
        var start = Node("start", GraphNodeKind.Start, 0, [NodeCatalog.OutExecPort()], []);
        var custom = Node(
            "custom",
            GraphNodeKind.Custom,
            1,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject
            {
                ["nodeType"] = "SetCustom",
                ["name"] = "custom",
                ["value"] = "custom-value"
            });
        var line = Node(
            "line",
            GraphNodeKind.Narration,
            2,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["text"] = "Custom node ran." });
        var end = Node("end", GraphNodeKind.EndScene, 3, [NodeCatalog.InExecPort()], []);

        return new GraphDocument(
            1,
            "custom-command",
            [start, custom, line, end],
            [
                new GraphEdge("e1", "start", "out", "custom", "in"),
                new GraphEdge("e2", "custom", "out", "line", "in"),
                new GraphEdge("e3", "line", "out", "end", "in")
            ]);
    }

    private static GraphDocument CreateCustomConditionGraph()
    {
        var start = Node("start", GraphNodeKind.Start, 0, [NodeCatalog.OutExecPort()], []);
        var custom = Node(
            "custom",
            GraphNodeKind.Custom,
            1,
            [
                NodeCatalog.InExecPort(),
                NodeCatalog.OutExecPort("true", "true"),
                NodeCatalog.OutExecPort("false", "false")
            ],
            new JsonObject
            {
                ["nodeType"] = "Gate",
                ["result"] = true
            });
        var trueLine = Node(
            "true_line",
            GraphNodeKind.Narration,
            2,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["text"] = "True path" });
        var falseLine = Node(
            "false_line",
            GraphNodeKind.Narration,
            3,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["text"] = "False path" });
        var end = Node("end", GraphNodeKind.EndScene, 4, [NodeCatalog.InExecPort()], []);

        return new GraphDocument(
            1,
            "custom-condition",
            [start, custom, trueLine, falseLine, end],
            [
                new GraphEdge("e1", "start", "out", "custom", "in"),
                new GraphEdge("e2", "custom", "true", "true_line", "in"),
                new GraphEdge("e3", "custom", "false", "false_line", "in"),
                new GraphEdge("e4", "true_line", "out", "end", "in"),
                new GraphEdge("e5", "false_line", "out", "end", "in")
            ]);
    }

    private static StringTableLocalizer CreateKoreanLocalizer()
    {
        return new StringTableLocalizer(
            [
                new StringTableDocument(
                    "dialogue",
                    [
                        new StringTableEntry(
                            "speaker.mina",
                            new Dictionary<string, string> { ["ko"] = "미나", ["en"] = "Mina" }),
                        new StringTableEntry(
                            "line.greeting",
                            new Dictionary<string, string> { ["ko"] = "어서 와.", ["en"] = "Welcome." }),
                        new StringTableEntry(
                            "choice.left",
                            new Dictionary<string, string> { ["ko"] = "왼쪽", ["en"] = "Left" }),
                        new StringTableEntry(
                            "choice.right",
                            new Dictionary<string, string> { ["ko"] = "오른쪽", ["en"] = "Right" })
                    ])
            ],
            "ko",
            "en");
    }

    private static IReadOnlyDictionary<string, PompoGraphIR> CompileGraphLibrary(params GraphDocument[] graphs)
    {
        var compiler = new GraphCompiler();
        return graphs
            .Select(compiler.Compile)
            .ToDictionary(ir => ir.GraphId, StringComparer.Ordinal);
    }

    private static GraphNode Node(
        string id,
        GraphNodeKind kind,
        int column,
        IReadOnlyList<GraphPort> ports,
        JsonObject properties)
    {
        return new GraphNode(id, kind, new GraphPoint(column * 200, 0), ports, properties);
    }

    private sealed class TestCustomNodeHandler : IRuntimeCustomNodeHandler
    {
        public ValueTask<RuntimeCustomNodeResult> ExecuteAsync(
            RuntimeCustomNodeContext context,
            CancellationToken cancellationToken = default)
        {
            var nodeType = context.Arguments["nodeType"]?.GetValue<string>();
            if (nodeType == "Gate")
            {
                var result = context.Arguments["result"]?.GetValue<bool>() == true;
                return ValueTask.FromResult(new RuntimeCustomNodeResult(result ? "true" : "false"));
            }

            context.Variables[context.Arguments["name"]!.GetValue<string>()] =
                context.Arguments["value"]!.GetValue<string>();
            return ValueTask.FromResult(new RuntimeCustomNodeResult("out"));
        }
    }
}
