using System.Text.Json.Nodes;
using Pompo.Core.Project;
using Pompo.Runtime.Fna.Presentation;
using Pompo.Core.Runtime;
using Pompo.Core.Graphs;
using Pompo.VisualScripting;
using Pompo.VisualScripting.Runtime;

namespace Pompo.Tests;

public sealed class RuntimeUiLayoutTests
{
    [Fact]
    public void RuntimeUiThemeColors_ParsesProjectThemeWithFallbacks()
    {
        var colors = RuntimeUiThemeColors.FromProjectTheme(new PompoRuntimeUiTheme(
            CanvasClear: "#102030",
            DialogueBackground: "#11223344",
            Text: "invalid"));

        Assert.Equal(0x10, colors.CanvasClear.R);
        Assert.Equal(0x20, colors.CanvasClear.G);
        Assert.Equal(0x30, colors.CanvasClear.B);
        Assert.Equal(0xff, colors.CanvasClear.A);
        Assert.Equal(0x11, colors.DialogueBackground.R);
        Assert.Equal(0x22, colors.DialogueBackground.G);
        Assert.Equal(0x33, colors.DialogueBackground.B);
        Assert.Equal(0x44, colors.DialogueBackground.A);
        Assert.Equal(RuntimeUiThemeColors.Default.Text, colors.Text);
    }

    [Fact]
    public void RuntimeUiAnimationTiming_EvaluatesPanelFadeAndChoicePulse()
    {
        var settings = new PompoRuntimeUiAnimationSettings(
            Enabled: true,
            PanelFadeMilliseconds: 160,
            ChoicePulseMilliseconds: 900,
            ChoicePulseStrength: 0.2f);

        var firstFrame = RuntimeUiAnimationTiming.Evaluate(settings, 0);
        var halfwayFade = RuntimeUiAnimationTiming.Evaluate(settings, 0.08);
        var peakPulse = RuntimeUiAnimationTiming.Evaluate(settings, 0.225);
        var disabled = RuntimeUiAnimationTiming.Evaluate(settings with { Enabled = false }, 0);

        Assert.Equal(0f, firstFrame.PanelOpacity);
        Assert.Equal(0.5f, halfwayFade.PanelOpacity, precision: 2);
        Assert.Equal(1.2f, peakPulse.SelectedChoiceScale, precision: 2);
        Assert.Equal(1f, disabled.PanelOpacity);
        Assert.Equal(1f, disabled.SelectedChoiceScale);
    }

    [Fact]
    public void RuntimeTextReveal_RevealsTextOverTimeAndCanCompleteImmediately()
    {
        var reveal = new RuntimeTextReveal(new PompoRuntimeUiAnimationSettings(
            TextRevealCharactersPerSecond: 10));

        reveal.Update("Hello", 0);
        Assert.Equal(string.Empty, reveal.GetVisibleText("Hello"));
        Assert.False(reveal.IsComplete("Hello"));

        reveal.Update("Hello", 0.25);
        Assert.Equal("He", reveal.GetVisibleText("Hello"));

        Assert.True(reveal.Complete("Hello"));
        Assert.Equal("Hello", reveal.GetVisibleText("Hello"));
        Assert.True(reveal.IsComplete("Hello"));
    }

    [Fact]
    public void RuntimeTextReveal_DisabledOrZeroSpeedShowsTextImmediately()
    {
        var disabled = new RuntimeTextReveal(new PompoRuntimeUiAnimationSettings(
            Enabled: false,
            TextRevealCharactersPerSecond: 10));
        var instant = new RuntimeTextReveal(new PompoRuntimeUiAnimationSettings(
            TextRevealCharactersPerSecond: 0));

        disabled.Update("Instant", 0);
        instant.Update("Instant", 0);

        Assert.Equal("Instant", disabled.GetVisibleText("Instant"));
        Assert.Equal("Instant", instant.GetVisibleText("Instant"));
        Assert.True(disabled.IsComplete("Instant"));
        Assert.True(instant.IsComplete("Instant"));
    }

    [Fact]
    public void CreateFrame_MapsLineAndChoicesToStableVirtualRects()
    {
        var trace = new RuntimeTraceResult(
            "graph_intro",
            true,
            [
                new RuntimeTraceEvent("line", "graph_intro", 1, "Hello", "Mina"),
                new RuntimeTraceEvent(
                    "choice",
                    "graph_intro",
                    2,
                    Choices: ["Cafe", "Rooftop"],
                    SelectedChoice: "Rooftop")
            ],
            new Dictionary<string, object?> { ["route"] = "rooftop" },
            "bg-rooftop",
            [new RuntimeCharacterState("mina", "smile", RuntimeLayer.Character, 0.5f, 1f, true)],
            new RuntimeAudioState("bgm-rooftop", ["choice-click"]),
            ["Rooftop"]);

        var frame = new RuntimeUiLayout().CreateFrame(trace);

        Assert.Equal(1920, frame.VirtualWidth);
        Assert.Equal(1080, frame.VirtualHeight);
        Assert.NotNull(frame.Dialogue);
        Assert.Equal("Hello", frame.Dialogue.Text);
        Assert.Equal("Mina", frame.Dialogue.Speaker);
        Assert.Equal(new UiRect(120, 810, 1680, 190), frame.Dialogue.TextBox);
        Assert.Equal(2, frame.Dialogue.Choices.Count);
        Assert.True(frame.Dialogue.Choices[1].IsSelected);
        Assert.Equal("bg-rooftop", frame.BackgroundAssetId);
        Assert.Single(frame.Characters);
        Assert.Equal("bgm-rooftop", frame.Audio.BgmAssetId);
        Assert.Contains("choice-click", frame.Audio.PlayingSfxAssetIds);
        Assert.Null(frame.Audio.VoiceAssetId);
    }

    [Fact]
    public void CreateFrame_UsesProjectRuntimeUiLayoutSettings()
    {
        var trace = new RuntimeTraceResult(
            "graph_intro",
            true,
            [
                new RuntimeTraceEvent("line", "graph_intro", 1, "Hello", "Mina"),
                new RuntimeTraceEvent(
                    "choice",
                    "graph_intro",
                    2,
                    Choices: ["Cafe", "Rooftop"],
                    SelectedChoice: "Cafe")
            ],
            new Dictionary<string, object?>(),
            null,
            [],
            new RuntimeAudioState(null, []),
            []);
        var layout = new RuntimeUiLayout(new PompoRuntimeUiLayoutSettings
        {
            DialogueTextBox = new PompoRuntimeUiRect(100, 700, 1000, 160),
            DialogueNameBox = new PompoRuntimeUiRect(100, 640, 300, 50),
            ChoiceBoxWidth = 500,
            ChoiceBoxHeight = 48,
            ChoiceBoxSpacing = 8,
            SaveMenuBounds = new PompoRuntimeUiRect(1200, 40, 500, 640),
            SaveSlotHeight = 50,
            SaveSlotSpacing = 6,
            BacklogBounds = new PompoRuntimeUiRect(180, 100, 1200, 760)
        });

        var frame = layout.CreateFrame(trace, []);

        Assert.NotNull(frame.Dialogue);
        Assert.Equal(new UiRect(100, 700, 1000, 160), frame.Dialogue.TextBox);
        Assert.Equal(new UiRect(100, 640, 300, 50), frame.Dialogue.NameBox);
        Assert.Equal(500, frame.Dialogue.Choices[0].Bounds.Width);
        Assert.Equal(48, frame.Dialogue.Choices[0].Bounds.Height);
        Assert.NotNull(frame.SaveMenu);
        Assert.Equal(new UiRect(1200, 40, 500, 640), frame.SaveMenu.Bounds);
        Assert.Equal(50, frame.SaveMenu.QuickSlot.Bounds.Height);
    }

    [Fact]
    public void CreateFrame_ReturnsNoDialogueWhenTraceHasNoDisplayEvents()
    {
        var trace = new RuntimeTraceResult(
            "empty",
            true,
            [new RuntimeTraceEvent("complete", "empty", 0)],
            new Dictionary<string, object?>(),
            null,
            [],
            new RuntimeAudioState(null, []),
            []);

        var frame = new RuntimeUiLayout().CreateFrame(trace);

        Assert.Null(frame.Dialogue);
    }

    [Fact]
    public void CreateFrame_MapsSaveSlotsToOverlayWhenProvided()
    {
        var trace = new RuntimeTraceResult(
            "graph_intro",
            true,
            [new RuntimeTraceEvent("line", "graph_intro", 1, "Hello", "Mina")],
            new Dictionary<string, object?>(),
            null,
            [],
            new RuntimeAudioState(null, []),
            []);
        var saveSlots = Enumerable.Range(1, 7)
            .Select(index => new RuntimeSaveSlotMetadata(
                $"manual_{index}",
                $"Manual {index}",
                new DateTimeOffset(2026, 5, index, 12, 0, 0, TimeSpan.Zero),
                $"graph_{index}",
                $"node_{index}"))
            .Reverse()
            .ToArray();

        var frame = new RuntimeUiLayout().CreateFrame(trace, saveSlots);

        Assert.NotNull(frame.SaveMenu);
        Assert.Equal("Save / Load", frame.SaveMenu.Title);
        Assert.Equal(new UiRect(1260, 60, 560, 720), frame.SaveMenu.Bounds);
        Assert.Equal("quick", frame.SaveMenu.QuickSlot.SlotId);
        Assert.True(frame.SaveMenu.QuickSlot.IsEmpty);
        Assert.Equal(6, frame.SaveMenu.Slots.Count);
        Assert.Equal("manual_1", frame.SaveMenu.Slots[0].SlotId);
        Assert.Equal("graph_1:node_1", frame.SaveMenu.Slots[0].Location);
        Assert.True(frame.SaveMenu.Slots[0].IsSelected);
        Assert.DoesNotContain(frame.SaveMenu.Slots, slot => slot.SlotId == "manual_7");
        Assert.All(frame.SaveMenu.Slots, slot => Assert.True(slot.Bounds.Bottom < frame.SaveMenu.Bounds.Bottom));
        Assert.Contains("Mouse hover select", frame.SaveMenu.HelpText);
        Assert.Contains("Click load", frame.SaveMenu.HelpText);
        Assert.Contains("F5 quick save", frame.SaveMenu.HelpText);
        Assert.Contains("F9 quick load", frame.SaveMenu.HelpText);
    }

    [Fact]
    public void RuntimeSaveMenuHitTest_ReturnsQuickAndManualSlotIds()
    {
        var trace = new RuntimeTraceResult(
            "graph_intro",
            true,
            [new RuntimeTraceEvent("line", "graph_intro", 1, "Hello", "Mina")],
            new Dictionary<string, object?>(),
            null,
            [],
            new RuntimeAudioState(null, []),
            []);
        var frame = new RuntimeUiLayout().CreateFrame(trace, []);
        var quick = frame.SaveMenu!.QuickSlot.Bounds;
        var manual = frame.SaveMenu.Slots[2].Bounds;

        Assert.Equal("quick", RuntimeSaveMenuHitTest.GetSlotIdAt(frame.SaveMenu, quick.X + 4, quick.Y + 4));
        Assert.Equal("manual_3", RuntimeSaveMenuHitTest.GetSlotIdAt(frame.SaveMenu, manual.X + 4, manual.Y + 4));
        Assert.Null(RuntimeSaveMenuHitTest.GetSlotIdAt(frame.SaveMenu, 1, 1));
    }

    [Fact]
    public void CreateFrame_MapsQuickSaveSlotToDedicatedOverlayRow()
    {
        var trace = new RuntimeTraceResult(
            "graph_intro",
            true,
            [new RuntimeTraceEvent("line", "graph_intro", 1, "Hello", "Mina")],
            new Dictionary<string, object?>(),
            null,
            [],
            new RuntimeAudioState(null, []),
            []);
        var saveSlots = new[]
        {
            new RuntimeSaveSlotMetadata(
                "quick",
                "Quick Save",
                new DateTimeOffset(2026, 5, 5, 12, 0, 0, TimeSpan.Zero),
                "graph_intro",
                "line_1")
        };

        var frame = new RuntimeUiLayout().CreateFrame(trace, saveSlots);

        Assert.NotNull(frame.SaveMenu);
        Assert.False(frame.SaveMenu.QuickSlot.IsEmpty);
        Assert.Equal("graph_intro:line_1", frame.SaveMenu.QuickSlot.Location);
        Assert.Equal(new UiRect(1288, 142, 504, 60), frame.SaveMenu.QuickSlot.Bounds);
        Assert.All(frame.SaveMenu.Slots, slot => Assert.False(slot.IsSelected && slot.SlotId == "quick"));
    }

    [Fact]
    public void CreateFrame_CreatesEmptySaveOverlayWhenSaveRootHasNoSlots()
    {
        var trace = new RuntimeTraceResult(
            "empty",
            true,
            [new RuntimeTraceEvent("complete", "empty", 0)],
            new Dictionary<string, object?>(),
            null,
            [],
            new RuntimeAudioState(null, []),
            []);

        var frame = new RuntimeUiLayout().CreateFrame(trace, []);

        Assert.NotNull(frame.SaveMenu);
        Assert.Equal(6, frame.SaveMenu.Slots.Count);
        Assert.True(frame.SaveMenu.QuickSlot.IsEmpty);
        Assert.All(frame.SaveMenu.Slots, slot => Assert.True(slot.IsEmpty));
        Assert.Equal("Empty", frame.SaveMenu.Slots[0].Location);
        Assert.Equal("No save data", frame.SaveMenu.Slots[0].SavedAt);
    }

    [Fact]
    public void CreateFrame_SelectsRequestedManualSaveSlot()
    {
        var trace = new RuntimeTraceResult(
            "empty",
            true,
            [new RuntimeTraceEvent("complete", "empty", 0)],
            new Dictionary<string, object?>(),
            null,
            [],
            new RuntimeAudioState(null, []),
            []);

        var frame = new RuntimeUiLayout().CreateFrame(trace, [], selectedSaveSlotId: "manual_4");

        Assert.NotNull(frame.SaveMenu);
        Assert.True(frame.SaveMenu.Slots[3].IsSelected);
        Assert.Equal("manual_4", frame.SaveMenu.Slots[3].SlotId);
        Assert.All(frame.SaveMenu.Slots.Where(slot => slot.SlotId != "manual_4"), slot => Assert.False(slot.IsSelected));
    }

    [Fact]
    public void CreateFrame_MapsRestoredRuntimeSnapshotToVisualFrame()
    {
        var saveData = new RuntimeSaveData(
            1,
            "graph_intro",
            "save_after_choice",
            [],
            new Dictionary<string, object?> { ["route"] = "cafe" },
            "bg-cafe",
            [new RuntimeCharacterState("mina", "smile", RuntimeLayer.CharacterFront, 0.45f, 1f, true)],
            new RuntimeAudioState("bgm-cafe", ["sfx-chime"]),
            ["Cafe"]);
        var snapshot = new RuntimeExecutionSnapshot(
            "graph_intro",
            4,
            false,
            saveData.Variables,
            saveData.BackgroundAssetId,
            saveData.Characters,
            saveData.Audio,
            null,
            [],
            saveData.ChoiceHistory);

        var frame = new RuntimeUiLayout().CreateFrame(snapshot, [], saveData);

        Assert.Equal("bg-cafe", frame.BackgroundAssetId);
        Assert.Single(frame.Characters);
        Assert.Equal(RuntimeLayer.CharacterFront, frame.Characters[0].Layer);
        Assert.Equal("bgm-cafe", frame.Audio.BgmAssetId);
        Assert.Same(saveData, frame.CurrentSaveData);
        Assert.NotNull(frame.SaveMenu);
    }

    [Fact]
    public void CreateFrame_MapsTraceLinesToBacklogOverlay()
    {
        var trace = new RuntimeTraceResult(
            "graph_intro",
            true,
            Enumerable.Range(1, 10)
                .Select(index => new RuntimeTraceEvent(
                    "line",
                    "graph_intro",
                    index,
                    $"Line {index}",
                    index % 2 == 0 ? "Mina" : null))
                .Append(new RuntimeTraceEvent("complete", "graph_intro", 11))
                .ToArray(),
            new Dictionary<string, object?>(),
            null,
            [],
            new RuntimeAudioState(null, []),
            []);

        var frame = new RuntimeUiLayout().CreateFrame(trace);

        Assert.NotNull(frame.Backlog);
        Assert.Equal("Backlog", frame.Backlog.Title);
        Assert.Equal(new UiRect(260, 120, 1400, 840), frame.Backlog.Bounds);
        Assert.Equal(8, frame.Backlog.Lines.Count);
        Assert.Equal("Line 3", frame.Backlog.Lines[0].Text);
        Assert.Equal("Line 10", frame.Backlog.Lines[^1].Text);
        Assert.Equal("Mina", frame.Backlog.Lines[^1].Speaker);
    }

    [Fact]
    public void Wrap_SplitsTextIntoStableLineCount()
    {
        var lines = RuntimeTextLayout.Wrap(
            "A small bell rings as the story begins.",
            maxCharactersPerLine: 14,
            maxLines: 3);

        Assert.Equal(3, lines.Count);
        Assert.All(lines, line => Assert.True(line.Length <= 14));
        Assert.Equal("A small bell", lines[0]);
    }

    [Fact]
    public void TinyBitmapFont_MeasuresTextAndLineHeightWithoutContentPipeline()
    {
        var font = new TinyBitmapFont();

        Assert.Equal(30, font.MeasureWidth("Pompo", scale: 1));
        Assert.Equal(18, font.LineHeight(scale: 2));
    }

    [Fact]
    public void RuntimePlaySession_AdvancesOneDisplayStopAtATime()
    {
        var session = new RuntimePlaySession(new GraphCompiler().Compile(CreateInteractiveGraph()));

        Assert.Equal("Hello", session.Frame.Dialogue?.Text);
        Assert.False(session.Snapshot.IsComplete);

        session.AdvanceOrChoose();

        Assert.Equal(2, session.Frame.Dialogue?.Choices.Count);
        Assert.True(session.HasChoices);

        session.SelectChoiceRelative(1);
        Assert.Equal(1, session.SelectedChoiceIndex);
        Assert.True(session.Frame.Dialogue!.Choices[1].IsSelected);

        session.AdvanceOrChoose();

        Assert.Equal("Right route", session.Frame.Dialogue?.Text);
        Assert.Contains("Right", session.Snapshot.ChoiceHistory);
        Assert.Equal("right", session.Snapshot.Variables["route"]);
        Assert.Equal(2, session.Frame.Backlog?.Lines.Count);

        session.AdvanceOrChoose();

        Assert.True(session.Snapshot.IsComplete);
    }

    [Fact]
    public void RuntimePlaySession_UsesProjectRuntimeUiLayoutSettingsAcrossStops()
    {
        var layout = new PompoRuntimeUiLayoutSettings
        {
            DialogueTextBox = new PompoRuntimeUiRect(80, 720, 1400, 150),
            DialogueNameBox = new PompoRuntimeUiRect(90, 660, 320, 48),
            ChoiceBoxWidth = 520,
            ChoiceBoxHeight = 44,
            ChoiceBoxSpacing = 12
        };
        var session = new RuntimePlaySession(
            new GraphCompiler().Compile(CreateInteractiveGraph()),
            runtimeUiLayout: layout);

        Assert.Equal(new UiRect(80, 720, 1400, 150), session.Frame.Dialogue?.TextBox);

        session.AdvanceOrChoose();

        Assert.Equal(2, session.Frame.Dialogue?.Choices.Count);
        Assert.Equal(520, session.Frame.Dialogue!.Choices[0].Bounds.Width);
        Assert.Equal(44, session.Frame.Dialogue.Choices[0].Bounds.Height);
        Assert.Equal(12, session.Frame.Dialogue.Choices[1].Bounds.Y - session.Frame.Dialogue.Choices[0].Bounds.Bottom);
    }

    [Fact]
    public void RuntimePlaySession_ClickingChoiceSelectsAndAdvances()
    {
        var session = new RuntimePlaySession(new GraphCompiler().Compile(CreateInteractiveGraph()));
        session.AdvanceOrChoose();
        var secondChoice = session.Frame.Dialogue!.Choices[1].Bounds;

        session.ChooseAtVirtualPoint(secondChoice.X + 10, secondChoice.Y + 10);

        Assert.Equal("Right route", session.Frame.Dialogue?.Text);
        Assert.Equal("right", session.Snapshot.Variables["route"]);
    }

    [Fact]
    public void RuntimePlaySession_HoveringChoiceUpdatesSelectionWithoutAdvancing()
    {
        var session = new RuntimePlaySession(new GraphCompiler().Compile(CreateInteractiveGraph()));
        session.AdvanceOrChoose();
        var secondChoice = session.Frame.Dialogue!.Choices[1].Bounds;

        var changed = session.SelectChoiceAtVirtualPoint(secondChoice.X + 10, secondChoice.Y + 10);

        Assert.True(changed);
        Assert.Equal(1, session.SelectedChoiceIndex);
        Assert.True(session.Frame.Dialogue!.Choices[1].IsSelected);
        Assert.True(session.HasChoices);
        Assert.DoesNotContain("route", session.Snapshot.Variables.Keys);
    }

    [Fact]
    public void RuntimePlaySession_SkipsDisabledChoicesForSelectionAndClicking()
    {
        var session = new RuntimePlaySession(new GraphCompiler().Compile(CreateDisabledChoiceGraph()));
        var disabledChoice = session.Frame.Dialogue!.Choices[0].Bounds;

        Assert.True(session.HasChoices);
        Assert.False(session.Frame.Dialogue.Choices[0].IsEnabled);
        Assert.True(session.Frame.Dialogue.Choices[1].IsEnabled);
        Assert.Equal(1, session.SelectedChoiceIndex);
        Assert.True(session.Frame.Dialogue.Choices[1].IsSelected);

        var changed = session.SelectChoiceAtVirtualPoint(disabledChoice.X + 10, disabledChoice.Y + 10);
        session.ChooseAtVirtualPoint(disabledChoice.X + 10, disabledChoice.Y + 10);

        Assert.False(changed);
        Assert.True(session.HasChoices);
        Assert.DoesNotContain("route", session.Snapshot.Variables.Keys);
    }

    private static GraphDocument CreateInteractiveGraph()
    {
        var start = Node("start", GraphNodeKind.Start, 0, [NodeCatalog.OutExecPort()], []);
        var line = Node(
            "line",
            GraphNodeKind.Narration,
            1,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["text"] = "Hello" });
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
                ["choices"] = new JsonArray
                {
                    new JsonObject { ["text"] = "Left", ["port"] = "left" },
                    new JsonObject { ["text"] = "Right", ["port"] = "right" }
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
        var rightLine = Node(
            "right_line",
            GraphNodeKind.Narration,
            5,
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["text"] = "Right route" });
        var end = Node("end", GraphNodeKind.EndScene, 6, [NodeCatalog.InExecPort()], []);

        return new GraphDocument(
            1,
            "interactive",
            [start, line, choice, left, right, rightLine, end],
            [
                new GraphEdge("e1", "start", "out", "line", "in"),
                new GraphEdge("e2", "line", "out", "choice", "in"),
                new GraphEdge("e3", "choice", "left", "left", "in"),
                new GraphEdge("e4", "choice", "right", "right", "in"),
                new GraphEdge("e5", "left", "out", "end", "in"),
                new GraphEdge("e6", "right", "out", "right_line", "in"),
                new GraphEdge("e7", "right_line", "out", "end", "in")
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

    private static GraphNode Node(
        string nodeId,
        GraphNodeKind kind,
        int index,
        IReadOnlyList<GraphPort> ports,
        JsonObject properties)
    {
        return new GraphNode(nodeId, kind, new GraphPoint(index * 220, 0), ports, properties);
    }
}
