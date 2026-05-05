using System.Text.Json.Nodes;
using Pompo.Core.Graphs;
using Pompo.Editor.Avalonia.ViewModels;
using Pompo.VisualScripting;
using Pompo.VisualScripting.Authoring;

namespace Pompo.Tests;

public sealed class GraphEditorViewModelTests
{
    [Fact]
    public void GraphEditorViewModel_TracksSelectionDirtyStateAndValidation()
    {
        var editor = new GraphEditorViewModel(new GraphAuthoringService().CreateEmpty("editor"));

        editor.AddNode(GraphNodeKind.Start, "start", 0, 0);
        editor.AddNode(GraphNodeKind.Narration, "line", 200, 0);
        editor.AddNode(GraphNodeKind.EndScene, "end", 420, 0);
        editor.Connect("e1", "start", "out", "line", "in");
        editor.Connect("e2", "line", "out", "end", "in");
        editor.SelectNode("line");
        editor.MoveSelectedNode(240, 12);
        editor.SetSelectedNodeProperties(new JsonObject { ["text"] = "Edited" });

        Assert.True(editor.IsDirty);
        Assert.True(editor.IsValid);
        Assert.Equal("line", editor.SelectedNodeId);
        Assert.True(editor.Nodes.Single(node => node.NodeId == "line").IsSelected);
        Assert.True(editor.CanvasNodes.Single(node => node.NodeId == "line").IsSelected);
        Assert.Equal(240, editor.Nodes.Single(node => node.NodeId == "line").X);
        Assert.Equal("Edited", editor.SelectedNode!.Properties["text"]!.GetValue<string>());
        Assert.Equal(GraphNodeKind.Narration, editor.SelectedNodeKind);
        Assert.Equal("Edited", editor.SelectedNodeText);
        Assert.Contains("\"text\": \"Edited\"", editor.SelectedNodePropertiesJson);

        editor.MarkSaved();
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void SelectedNodeText_UpdatesNodePropertiesAndDirtyState()
    {
        var editor = new GraphEditorViewModel(new GraphAuthoringService().CreateEmpty("text-edit"));
        editor.AddNode(GraphNodeKind.Narration, "line", 0, 0);

        editor.SelectedNodeText = "Updated in inspector";

        Assert.True(editor.IsDirty);
        Assert.Equal("Updated in inspector", editor.SelectedNodeText);
        Assert.Equal("Updated in inspector", editor.SelectedNode!.Properties["text"]!.GetValue<string>());
    }

    [Fact]
    public void ApplySelectedNodePropertiesJson_UpdatesArbitraryNodeProperties()
    {
        var editor = new GraphEditorViewModel(new GraphAuthoringService().CreateEmpty("raw-properties"));
        editor.AddNode(GraphNodeKind.ChangeBackground, "background", 0, 0);

        editor.SelectedNodePropertiesJson = """
        {
          "assetId": "bg_rooftop",
          "fadeSeconds": 0.5
        }
        """;
        var applied = editor.ApplySelectedNodePropertiesJson();

        Assert.True(applied);
        Assert.True(editor.IsDirty);
        Assert.Null(editor.SelectedNodePropertiesJsonError);
        Assert.Equal("bg_rooftop", editor.SelectedNode!.Properties["assetId"]!.GetValue<string>());
        Assert.Equal(0.5, editor.SelectedNode.Properties["fadeSeconds"]!.GetValue<double>());
        Assert.Contains("\"assetId\": \"bg_rooftop\"", editor.SelectedNodePropertiesJson);
    }

    [Fact]
    public void ApplySelectedNodePropertiesJson_InvalidJsonKeepsExistingProperties()
    {
        var editor = new GraphEditorViewModel(new GraphAuthoringService().CreateEmpty("raw-properties-invalid"));
        editor.AddNode(GraphNodeKind.Jump, "jump", 0, 0);
        editor.SetSelectedNodeProperties(new JsonObject { ["targetNodeId"] = "next" });
        editor.MarkSaved();

        editor.SelectedNodePropertiesJson = "{ invalid";
        var applied = editor.ApplySelectedNodePropertiesJson();

        Assert.False(applied);
        Assert.False(editor.IsDirty);
        Assert.StartsWith("Invalid JSON:", editor.SelectedNodePropertiesJsonError);
        Assert.Equal("next", editor.SelectedNode!.Properties["targetNodeId"]!.GetValue<string>());
        Assert.Equal("{ invalid", editor.SelectedNodePropertiesJson);
    }

    [Fact]
    public void ApplySelectedNodePropertiesJson_RejectsNonObjectJson()
    {
        var editor = new GraphEditorViewModel(new GraphAuthoringService().CreateEmpty("raw-properties-array"));
        editor.AddNode(GraphNodeKind.UnlockCg, "unlock", 0, 0);

        editor.SelectedNodePropertiesJson = "[\"cg_01\"]";
        var applied = editor.ApplySelectedNodePropertiesJson();

        Assert.False(applied);
        Assert.Equal("Node properties must be a JSON object.", editor.SelectedNodePropertiesJsonError);
        Assert.Equal(string.Empty, editor.SelectedNode!.Properties["cgId"]!.GetValue<string>());
    }

    [Fact]
    public void AddNode_SeedsDefaultPropertiesFromNodeCatalog()
    {
        var editor = new GraphEditorViewModel(new GraphAuthoringService().CreateEmpty("default-properties"));

        editor.AddNode(GraphNodeKind.ShowCharacter, "show", 0, 0);

        Assert.Equal(string.Empty, editor.SelectedNode!.Properties["characterId"]!.GetValue<string>());
        Assert.Equal("default", editor.SelectedNode.Properties["expressionId"]!.GetValue<string>());
        Assert.Equal(0.5, editor.SelectedNode.Properties["x"]!.GetValue<double>());
        Assert.Contains("\"characterId\": \"\"", editor.SelectedNodePropertiesJson);
    }

    [Fact]
    public void SelectedNodePropertyHints_ExposeRuntimePropertyContract()
    {
        var editor = new GraphEditorViewModel(new GraphAuthoringService().CreateEmpty("property-hints"));
        editor.AddNode(GraphNodeKind.CallGraph, "call", 0, 0);

        var hint = Assert.Single(editor.SelectedNodePropertyHints);

        Assert.Equal("graphId", hint.Name);
        Assert.Equal(GraphValueType.String, hint.ValueType);
        Assert.True(hint.IsRequired);
        Assert.Contains("Graph id", hint.Description);
    }

    [Fact]
    public void ConnectConnectionSourceToSelectedNode_CreatesExecutionEdge()
    {
        var editor = new GraphEditorViewModel(new GraphAuthoringService().CreateEmpty("connect-ui"));
        editor.AddNode(GraphNodeKind.Start, "start", 0, 0);
        editor.AddNode(GraphNodeKind.Narration, "line", 200, 0);

        editor.SelectNode("start");
        editor.MarkSelectedNodeAsConnectionSource();
        editor.SelectNode("line");
        editor.ConnectConnectionSourceToSelectedNode();

        var edge = Assert.Single(editor.Edges);
        Assert.Equal("start", edge.FromNodeId);
        Assert.Equal("out", edge.FromPortId);
        Assert.Equal("line", edge.ToNodeId);
        Assert.Equal("in", edge.ToPortId);
        Assert.True(editor.IsDirty);
        Assert.Equal("start", editor.ConnectionSourceNodeId);
    }

    [Fact]
    public void DisconnectSelectedNodeEdges_RemovesConnectedEdges()
    {
        var authoring = new GraphAuthoringService();
        var graph = authoring.CreateEmpty("disconnect-selected");
        graph = authoring.AddNode(graph, GraphNodeKind.Start, "start", new GraphPoint(0, 0));
        graph = authoring.AddNode(graph, GraphNodeKind.Narration, "line", new GraphPoint(200, 0));
        graph = authoring.AddNode(graph, GraphNodeKind.EndScene, "end", new GraphPoint(400, 0));
        graph = authoring.Connect(graph, "e1", "start", "out", "line", "in");
        graph = authoring.Connect(graph, "e2", "line", "out", "end", "in");
        var editor = new GraphEditorViewModel(graph);

        editor.SelectNode("line");
        var removed = editor.DisconnectSelectedNodeEdges();

        Assert.Equal(2, removed);
        Assert.Empty(editor.Edges);
        Assert.True(editor.IsDirty);
    }

    [Fact]
    public void DeleteSelectedNode_RemovesNodeEdgesSelectionAndSupportsUndo()
    {
        var authoring = new GraphAuthoringService();
        var graph = authoring.CreateEmpty("delete-selected");
        graph = authoring.AddNode(graph, GraphNodeKind.Start, "start", new GraphPoint(0, 0));
        graph = authoring.AddNode(graph, GraphNodeKind.Narration, "line", new GraphPoint(200, 0));
        graph = authoring.AddNode(graph, GraphNodeKind.EndScene, "end", new GraphPoint(400, 0));
        graph = authoring.Connect(graph, "e1", "start", "out", "line", "in");
        graph = authoring.Connect(graph, "e2", "line", "out", "end", "in");
        var editor = new GraphEditorViewModel(graph);
        editor.SelectNode("line");
        editor.MarkSelectedNodeAsConnectionSource();

        var deleted = editor.DeleteSelectedNode();

        Assert.True(deleted);
        Assert.True(editor.IsDirty);
        Assert.Null(editor.SelectedNodeId);
        Assert.Null(editor.ConnectionSourceNodeId);
        Assert.DoesNotContain(editor.Nodes, node => node.NodeId == "line");
        Assert.Empty(editor.Edges);

        Assert.True(editor.Undo());
        Assert.Equal("line", editor.SelectedNodeId);
        Assert.Equal("line", editor.ConnectionSourceNodeId);
        Assert.Contains(editor.Nodes, node => node.NodeId == "line");
        Assert.Equal(2, editor.Edges.Count);
    }

    [Fact]
    public void DuplicateSelectedNode_CopiesKindPropertiesOffsetsPositionAndSupportsUndo()
    {
        var editor = new GraphEditorViewModel(new GraphAuthoringService().CreateEmpty("duplicate-selected"));
        editor.AddNode(GraphNodeKind.Dialogue, "line", 120, 20);
        editor.SetSelectedNodeProperties(new JsonObject
        {
            ["speaker"] = "Mina",
            ["text"] = "Original"
        });
        editor.MarkSaved();

        var duplicateNodeId = editor.DuplicateSelectedNode();

        Assert.Equal("line_copy", duplicateNodeId);
        Assert.True(editor.IsDirty);
        Assert.Equal("line_copy", editor.SelectedNodeId);
        var duplicate = editor.SelectedNode!;
        Assert.Equal(GraphNodeKind.Dialogue, duplicate.Kind);
        Assert.Equal(new GraphPoint(200, 60), duplicate.Position);
        Assert.Equal("Mina", duplicate.Properties["speaker"]!.GetValue<string>());
        Assert.Equal("Original", duplicate.Properties["text"]!.GetValue<string>());
        Assert.Empty(editor.Edges);

        Assert.True(editor.Undo());
        Assert.Equal("line", editor.SelectedNodeId);
        Assert.Single(editor.Nodes);
        Assert.DoesNotContain(editor.Nodes, node => node.NodeId == "line_copy");
    }

    [Fact]
    public void GraphEditorViewModel_ExposesDiagnosticsForInvalidGraph()
    {
        var editor = new GraphEditorViewModel(new GraphAuthoringService().CreateEmpty("invalid"));

        Assert.False(editor.IsValid);
        Assert.Contains(editor.Diagnostics, diagnostic => diagnostic.Code == "GRAPH001");
    }

    [Fact]
    public void GraphEditorViewModel_DisconnectUpdatesValidity()
    {
        var authoring = new GraphAuthoringService();
        var graph = authoring.CreateEmpty("disconnect");
        graph = authoring.AddNode(graph, GraphNodeKind.Start, "start", new GraphPoint(0, 0));
        graph = authoring.AddNode(graph, GraphNodeKind.EndScene, "end", new GraphPoint(200, 0));
        graph = authoring.Connect(graph, "e1", "start", "out", "end", "in");
        var editor = new GraphEditorViewModel(graph);

        editor.Disconnect("e1");

        Assert.True(editor.IsDirty);
        Assert.False(editor.IsValid);
        Assert.Contains(editor.Diagnostics, diagnostic => diagnostic.Code == "GRAPH011");
    }

    [Fact]
    public void UndoRedo_RestoresGraphSelectionAndHistoryState()
    {
        var editor = new GraphEditorViewModel(new GraphAuthoringService().CreateEmpty("undo-redo"));
        editor.AddNode(GraphNodeKind.Narration, "line", 0, 0);
        editor.MoveSelectedNode(120, 12);
        editor.SetSelectedNodeProperties(new JsonObject { ["text"] = "Edited" });

        Assert.True(editor.CanUndo);
        Assert.False(editor.CanRedo);
        Assert.Equal("Edited", editor.SelectedNodeText);

        Assert.True(editor.Undo());
        Assert.Equal(string.Empty, editor.SelectedNodeText);
        Assert.Equal(120, editor.Nodes.Single(node => node.NodeId == "line").X);
        Assert.True(editor.CanRedo);

        Assert.True(editor.Undo());
        Assert.Equal(0, editor.Nodes.Single(node => node.NodeId == "line").X);

        Assert.True(editor.Redo());
        Assert.Equal(120, editor.Nodes.Single(node => node.NodeId == "line").X);
        Assert.True(editor.CanUndo);
        Assert.True(editor.CanRedo);
        Assert.Equal("line", editor.SelectedNodeId);
    }

    [Fact]
    public void NewEditAfterUndoClearsRedoStack()
    {
        var editor = new GraphEditorViewModel(new GraphAuthoringService().CreateEmpty("redo-clear"));
        editor.AddNode(GraphNodeKind.Narration, "line", 0, 0);
        editor.MoveSelectedNode(120, 0);
        Assert.True(editor.Undo());
        Assert.True(editor.CanRedo);

        editor.MoveSelectedNode(240, 0);

        Assert.False(editor.CanRedo);
        Assert.False(editor.Redo());
        Assert.Equal(240, editor.Nodes.Single(node => node.NodeId == "line").X);
    }

    [Fact]
    public void CanvasNodesAndEdgesExposeVisualGraphGeometry()
    {
        var authoring = new GraphAuthoringService();
        var graph = authoring.CreateEmpty("surface");
        graph = authoring.AddNode(graph, GraphNodeKind.Start, "start", new GraphPoint(0, -80));
        graph = authoring.AddNode(graph, GraphNodeKind.Narration, "line", new GraphPoint(260, 20));
        graph = authoring.Connect(graph, "e1", "start", "out", "line", "in");
        var editor = new GraphEditorViewModel(graph);

        var start = editor.CanvasNodes.Single(node => node.NodeId == "start");
        var line = editor.CanvasNodes.Single(node => node.NodeId == "line");
        var edge = Assert.Single(editor.CanvasEdges);

        Assert.True(start.X < line.X);
        Assert.True(start.Y < line.Y);
        Assert.Equal(start.X + start.Width, edge.StartX);
        Assert.Equal(line.X, edge.EndX);
        Assert.True(editor.CanvasWidth >= line.X + line.Width);
        Assert.True(editor.CanvasHeight >= line.Y + line.Height);
    }

    [Fact]
    public void MoveNodeFromCanvas_UpdatesGraphCoordinatesAndSupportsUndo()
    {
        var editor = new GraphEditorViewModel(new GraphAuthoringService().CreateEmpty("drag"));
        editor.AddNode(GraphNodeKind.Narration, "line", 100, 50);
        var original = editor.SelectedNode!.Position;
        var canvasNode = editor.CanvasNodes.Single(node => node.NodeId == "line");

        editor.MoveNodeFromCanvas("line", canvasNode.X + 84, canvasNode.Y + 42);

        Assert.Equal("line", editor.SelectedNodeId);
        Assert.True(editor.SelectedNode!.Position.X > original.X);
        Assert.True(editor.SelectedNode.Position.Y > original.Y);
        Assert.True(editor.CanUndo);

        Assert.True(editor.Undo());
        Assert.Equal(original, editor.SelectedNode!.Position);
    }

    [Fact]
    public void ActivateCanvasNode_SelectsSourceThenConnectsTarget()
    {
        var editor = new GraphEditorViewModel(new GraphAuthoringService().CreateEmpty("canvas-connect"));
        editor.AddNode(GraphNodeKind.Start, "start", 0, 0);
        editor.AddNode(GraphNodeKind.Narration, "line", 200, 0);

        var firstClickConnected = editor.ActivateCanvasNode("start");
        var secondClickConnected = editor.ActivateCanvasNode("line");

        Assert.False(firstClickConnected);
        Assert.True(secondClickConnected);
        Assert.Null(editor.ConnectionSourceNodeId);
        var edge = Assert.Single(editor.Edges);
        Assert.Equal("start", edge.FromNodeId);
        Assert.Equal("line", edge.ToNodeId);
        Assert.True(editor.IsDirty);
    }
}
