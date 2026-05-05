using System.Text.Json.Nodes;
using Pompo.Core.Graphs;
using Pompo.VisualScripting;

namespace Pompo.Tests;

internal static class GraphFixtures
{
    public static GraphDocument LinearGraph()
    {
        var start = new GraphNode(
            "start",
            GraphNodeKind.Start,
            new GraphPoint(0, 0),
            [NodeCatalog.OutExecPort()],
            []);

        var dialogue = new GraphNode(
            "line-1",
            GraphNodeKind.Dialogue,
            new GraphPoint(220, 0),
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject
            {
                ["speaker"] = "Pompo",
                ["text"] = "Hello"
            });

        var end = new GraphNode(
            "end",
            GraphNodeKind.EndScene,
            new GraphPoint(440, 0),
            [NodeCatalog.InExecPort()],
            []);

        return new GraphDocument(
            1,
            "intro",
            [start, dialogue, end],
            [
                new GraphEdge("e1", "start", "out", "line-1", "in"),
                new GraphEdge("e2", "line-1", "out", "end", "in")
            ]);
    }
}
