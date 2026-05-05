using System.Text.Json.Nodes;
using Pompo.Core.Graphs;
using Pompo.VisualScripting.Authoring;

namespace Pompo.Tests;

public sealed class GraphAuthoringServiceTests
{
    [Fact]
    public void AddMoveConnectAndSetProperties_ProducesValidGraph()
    {
        var authoring = new GraphAuthoringService();
        var graph = authoring.CreateEmpty("authoring");

        graph = authoring.AddNode(graph, GraphNodeKind.Start, "start", new GraphPoint(0, 0));
        graph = authoring.AddNode(
            graph,
            GraphNodeKind.Narration,
            "line",
            new GraphPoint(240, 0),
            new JsonObject { ["text"] = "Draft" });
        graph = authoring.AddNode(graph, GraphNodeKind.EndScene, "end", new GraphPoint(520, 0));
        graph = authoring.MoveNode(graph, "line", new GraphPoint(280, 12));
        graph = authoring.SetNodeProperties(graph, "line", new JsonObject { ["text"] = "Ready" });
        graph = authoring.Connect(graph, "e1", "start", "out", "line", "in");
        graph = authoring.Connect(graph, "e2", "line", "out", "end", "in");

        Assert.True(authoring.Validate(graph).IsValid);
        Assert.Equal(new GraphPoint(280, 12), graph.Nodes.Single(node => node.NodeId == "line").Position);
        Assert.Equal("Ready", graph.Nodes.Single(node => node.NodeId == "line").Properties["text"]!.GetValue<string>());
    }

    [Fact]
    public void Connect_RejectsInvalidPortDirection()
    {
        var authoring = new GraphAuthoringService();
        var graph = authoring.CreateEmpty("invalid");
        graph = authoring.AddNode(graph, GraphNodeKind.Start, "start", new GraphPoint(0, 0));
        graph = authoring.AddNode(graph, GraphNodeKind.EndScene, "end", new GraphPoint(200, 0));

        Assert.Throws<InvalidOperationException>(() =>
            authoring.Connect(graph, "bad", "end", "in", "start", "out"));
    }

    [Fact]
    public void AddNode_SeedsChoiceWithTwoConnectedAuthoringPorts()
    {
        var authoring = new GraphAuthoringService();
        var graph = authoring.CreateEmpty("choice-authoring");

        graph = authoring.AddNode(graph, GraphNodeKind.Choice, "choice", new GraphPoint(0, 0));

        var choice = graph.Nodes.Single(node => node.NodeId == "choice");
        Assert.Contains(choice.Ports, port => port.PortId == "left" && !port.IsInput);
        Assert.Contains(choice.Ports, port => port.PortId == "right" && !port.IsInput);

        var choices = Assert.IsType<JsonArray>(choice.Properties["choices"]);
        Assert.Equal("left", choices[0]!["port"]!.GetValue<string>());
        Assert.Equal("right", choices[1]!["port"]!.GetValue<string>());
    }

    [Fact]
    public void DeleteNode_RemovesNodeAndConnectedEdges()
    {
        var authoring = new GraphAuthoringService();
        var graph = authoring.CreateEmpty("delete");
        graph = authoring.AddNode(graph, GraphNodeKind.Start, "start", new GraphPoint(0, 0));
        graph = authoring.AddNode(graph, GraphNodeKind.Narration, "line", new GraphPoint(200, 0));
        graph = authoring.AddNode(graph, GraphNodeKind.EndScene, "end", new GraphPoint(400, 0));
        graph = authoring.Connect(graph, "e1", "start", "out", "line", "in");
        graph = authoring.Connect(graph, "e2", "line", "out", "end", "in");

        graph = authoring.DeleteNode(graph, "line");

        Assert.DoesNotContain(graph.Nodes, node => node.NodeId == "line");
        Assert.Empty(graph.Edges);
        Assert.Throws<InvalidOperationException>(() => authoring.DeleteNode(graph, "line"));
    }

    [Fact]
    public async Task GraphDocumentFileService_RoundTripsGraphJson()
    {
        var root = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "Graphs", "roundtrip.pompo-graph.json");
        var authoring = new GraphAuthoringService();
        var graph = authoring.CreateEmpty("roundtrip");
        graph = authoring.AddNode(graph, GraphNodeKind.Start, "start", new GraphPoint(0, 0));
        graph = authoring.AddNode(graph, GraphNodeKind.EndScene, "end", new GraphPoint(200, 0));
        graph = authoring.Connect(graph, "e1", "start", "out", "end", "in");

        var files = new GraphDocumentFileService();
        await files.SaveAsync(path, graph);
        var loaded = await files.LoadAsync(path);

        Assert.Equal(graph.GraphId, loaded.GraphId);
        Assert.Equal(graph.Nodes.Count, loaded.Nodes.Count);
        Assert.Equal(graph.Edges.Count, loaded.Edges.Count);
    }
}
