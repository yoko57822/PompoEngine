using System.Text.Json.Nodes;
using Pompo.Core.Graphs;
using Pompo.VisualScripting;

namespace Pompo.Tests;

public sealed class GraphValidationTests
{
    [Fact]
    public void Validate_ReportsMissingStart()
    {
        var graph = new GraphDocument(1, "missing-start", [], []);

        var result = new GraphValidator().Validate(graph);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "GRAPH001");
    }

    [Fact]
    public void Compile_EmitsReachableInstructions()
    {
        var ir = new GraphCompiler().Compile(GraphFixtures.LinearGraph());

        Assert.Equal("intro", ir.GraphId);
        Assert.Equal([GraphNodeKind.Start, GraphNodeKind.Dialogue, GraphNodeKind.EndScene], ir.Instructions.Select(instruction => instruction.Operation));
        Assert.Equal(1, ir.Instructions[0].Jumps["out"]);
    }

    [Fact]
    public void Validate_ReportsMissingRequiredNodeProperty()
    {
        var graph = CreateLinearGraph(
            new GraphNode(
                "sfx",
                GraphNodeKind.PlaySfx,
                new GraphPoint(220, 0),
                [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
                new JsonObject { ["assetId"] = "" }));

        var result = new GraphValidator().Validate(graph);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "GRAPH013" && diagnostic.NodeId == "sfx");
    }

    [Fact]
    public void Validate_AcceptsRequiredPropertyAlternativeNames()
    {
        var graph = CreateLinearGraph(
            new GraphNode(
                "line",
                GraphNodeKind.Narration,
                new GraphPoint(220, 0),
                [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
                new JsonObject { ["textKey"] = "intro.opening" }));

        var result = new GraphValidator().Validate(graph);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReportsChoiceEntryWithMissingOrDisconnectedPort()
    {
        var start = new GraphNode(
            "start",
            GraphNodeKind.Start,
            new GraphPoint(0, 0),
            [NodeCatalog.OutExecPort()],
            []);
        var choice = new GraphNode(
            "choice",
            GraphNodeKind.Choice,
            new GraphPoint(220, 0),
            [
                NodeCatalog.InExecPort(),
                NodeCatalog.OutExecPort("choice", "choice"),
                NodeCatalog.OutExecPort("left", "left")
            ],
            new JsonObject
            {
                ["choices"] = new JsonArray
                {
                    new JsonObject { ["text"] = "Left", ["port"] = "left" },
                    new JsonObject { ["text"] = "Missing", ["port"] = "missing" },
                    new JsonObject { ["text"] = "Disconnected", ["port"] = "choice" }
                }
            });
        var left = new GraphNode(
            "left",
            GraphNodeKind.EndScene,
            new GraphPoint(440, 0),
            [NodeCatalog.InExecPort()],
            []);

        var graph = new GraphDocument(
            1,
            "choice-validation",
            [start, choice, left],
            [
                new GraphEdge("e1", "start", "out", "choice", "in"),
                new GraphEdge("e2", "choice", "left", "left", "in")
            ]);

        var result = new GraphValidator().Validate(graph);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "GRAPH014" && diagnostic.PortId == "missing");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "GRAPH014" && diagnostic.PortId == "choice");
    }

    [Fact]
    public void Validate_ReportsChoiceNodeWithOnlyStaticallyDisabledChoices()
    {
        var start = new GraphNode(
            "start",
            GraphNodeKind.Start,
            new GraphPoint(0, 0),
            [NodeCatalog.OutExecPort()],
            []);
        var choice = new GraphNode(
            "choice",
            GraphNodeKind.Choice,
            new GraphPoint(220, 0),
            [
                NodeCatalog.InExecPort(),
                NodeCatalog.OutExecPort("locked", "locked"),
                NodeCatalog.OutExecPort("also_locked", "also_locked")
            ],
            new JsonObject
            {
                ["choices"] = new JsonArray
                {
                    new JsonObject { ["text"] = "Locked", ["port"] = "locked", ["enabled"] = false },
                    new JsonObject { ["text"] = "Also locked", ["port"] = "also_locked", ["enabled"] = false }
                }
            });
        var locked = new GraphNode(
            "locked",
            GraphNodeKind.EndScene,
            new GraphPoint(440, 0),
            [NodeCatalog.InExecPort()],
            []);
        var alsoLocked = new GraphNode(
            "also_locked",
            GraphNodeKind.EndScene,
            new GraphPoint(440, 120),
            [NodeCatalog.InExecPort()],
            []);
        var graph = new GraphDocument(
            1,
            "choice-disabled-validation",
            [start, choice, locked, alsoLocked],
            [
                new GraphEdge("e1", "start", "out", "choice", "in"),
                new GraphEdge("e2", "choice", "locked", "locked", "in"),
                new GraphEdge("e3", "choice", "also_locked", "also_locked", "in")
            ]);

        var result = new GraphValidator().Validate(graph);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "GRAPH015" && diagnostic.NodeId == "choice");
    }

    [Fact]
    public void Validate_ReportsMalformedChoiceEntriesInsteadOfThrowing()
    {
        var start = new GraphNode(
            "start",
            GraphNodeKind.Start,
            new GraphPoint(0, 0),
            [NodeCatalog.OutExecPort()],
            []);
        var choice = new GraphNode(
            "choice",
            GraphNodeKind.Choice,
            new GraphPoint(220, 0),
            [
                NodeCatalog.InExecPort(),
                NodeCatalog.OutExecPort("left", "left")
            ],
            new JsonObject
            {
                ["choices"] = new JsonArray
                {
                    "not-an-object",
                    new JsonObject { ["text"] = "Bad port", ["port"] = 42 },
                    new JsonObject { ["text"] = "Left", ["port"] = "left" }
                }
            });
        var left = new GraphNode(
            "left",
            GraphNodeKind.EndScene,
            new GraphPoint(440, 0),
            [NodeCatalog.InExecPort()],
            []);
        var graph = new GraphDocument(
            1,
            "choice-malformed-validation",
            [start, choice, left],
            [
                new GraphEdge("e1", "start", "out", "choice", "in"),
                new GraphEdge("e2", "choice", "left", "left", "in")
            ]);

        var result = new GraphValidator().Validate(graph);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "GRAPH016" && diagnostic.NodeId == "choice");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "GRAPH014" && diagnostic.NodeId == "choice");
    }

    private static GraphDocument CreateLinearGraph(GraphNode middle)
    {
        var start = new GraphNode(
            "start",
            GraphNodeKind.Start,
            new GraphPoint(0, 0),
            [NodeCatalog.OutExecPort()],
            []);
        var end = new GraphNode(
            "end",
            GraphNodeKind.EndScene,
            new GraphPoint(440, 0),
            [NodeCatalog.InExecPort()],
            []);

        return new GraphDocument(
            1,
            "validation",
            [start, middle, end],
            [
                new GraphEdge("e1", "start", "out", middle.NodeId, "in"),
                new GraphEdge("e2", middle.NodeId, "out", "end", "in")
            ]);
    }
}
