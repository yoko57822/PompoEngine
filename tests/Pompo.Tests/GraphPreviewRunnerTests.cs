using System.Text.Json.Nodes;
using Pompo.Core;
using Pompo.Core.Graphs;
using Pompo.Core.Localization;
using Pompo.Core.Project;
using Pompo.Editor.Avalonia.Services;
using Pompo.VisualScripting;

namespace Pompo.Tests;

public sealed class GraphPreviewRunnerTests
{
    [Fact]
    public async Task RuntimeProcessGraphPreviewRunner_RunsRuntimeCliAndReturnsTrace()
    {
        var ir = new GraphCompiler().Compile(GraphFixtures.LinearGraph());

        var result = await new RuntimeProcessGraphPreviewRunner().RunAsync(ir);

        Assert.True(result.Completed);
        Assert.Equal("intro", result.GraphId);
        Assert.Contains(result.Events, item => item.Kind == "line" && item.Text == "Hello");
    }

    [Fact]
    public async Task RuntimeProcessGraphPreviewRunner_PassesProjectLocaleToRuntimeCli()
    {
        var ir = new GraphCompiler().Compile(LocalizedGraph());
        var project = new PompoProjectDocument
        {
            ProjectName = "Localized Preview",
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
            ]
        };

        var result = await new RuntimeProcessGraphPreviewRunner().RunAsync(ir, project, "ko");

        Assert.True(result.Completed);
        Assert.Contains(result.Events, item => item.Kind == "line" && item.Text == "안녕");
    }

    [Fact]
    public async Task RuntimeProcessGraphPreviewRunner_WritesGraphLibraryForCallGraph()
    {
        var compiler = new GraphCompiler();
        var graphLibrary = new[] { CallGraphRoot(), CallGraphChild() }
            .Select(compiler.Compile)
            .ToDictionary(ir => ir.GraphId, StringComparer.Ordinal);

        var result = await new RuntimeProcessGraphPreviewRunner()
            .RunAsync(graphLibrary["root"], graphLibrary: graphLibrary);

        Assert.True(result.Completed);
        Assert.Contains(result.Events, item => item.Kind == "line" && item.GraphId == "child" && item.Text == "Child preview");
        Assert.Contains(result.Events, item => item.Kind == "line" && item.GraphId == "root" && item.Text == "Root after call");
    }

    private static GraphDocument LocalizedGraph()
    {
        var start = new GraphNode(
            "start",
            GraphNodeKind.Start,
            new GraphPoint(0, 0),
            [NodeCatalog.OutExecPort()],
            []);
        var line = new GraphNode(
            "line",
            GraphNodeKind.Narration,
            new GraphPoint(200, 0),
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject
            {
                ["tableId"] = "dialogue",
                ["textKey"] = "line.hello",
                ["text"] = "Hello"
            });
        var end = new GraphNode(
            "end",
            GraphNodeKind.EndScene,
            new GraphPoint(400, 0),
            [NodeCatalog.InExecPort()],
            []);

        return new GraphDocument(
            ProjectConstants.CurrentSchemaVersion,
            "localized",
            [start, line, end],
            [
                new GraphEdge("e1", "start", "out", "line", "in"),
                new GraphEdge("e2", "line", "out", "end", "in")
            ]);
    }

    private static GraphDocument CallGraphRoot()
    {
        var start = new GraphNode("start", GraphNodeKind.Start, new GraphPoint(0, 0), [NodeCatalog.OutExecPort()], []);
        var call = new GraphNode(
            "call_child",
            GraphNodeKind.CallGraph,
            new GraphPoint(200, 0),
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["graphId"] = "child" });
        var line = new GraphNode(
            "line",
            GraphNodeKind.Narration,
            new GraphPoint(400, 0),
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["text"] = "Root after call" });
        var end = new GraphNode("end", GraphNodeKind.EndScene, new GraphPoint(600, 0), [NodeCatalog.InExecPort()], []);

        return new GraphDocument(
            ProjectConstants.CurrentSchemaVersion,
            "root",
            [start, call, line, end],
            [
                new GraphEdge("e1", "start", "out", "call_child", "in"),
                new GraphEdge("e2", "call_child", "out", "line", "in"),
                new GraphEdge("e3", "line", "out", "end", "in")
            ]);
    }

    private static GraphDocument CallGraphChild()
    {
        var start = new GraphNode("start", GraphNodeKind.Start, new GraphPoint(0, 0), [NodeCatalog.OutExecPort()], []);
        var line = new GraphNode(
            "line",
            GraphNodeKind.Narration,
            new GraphPoint(200, 0),
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["text"] = "Child preview" });
        var ret = new GraphNode("return", GraphNodeKind.Return, new GraphPoint(400, 0), [NodeCatalog.InExecPort()], []);

        return new GraphDocument(
            ProjectConstants.CurrentSchemaVersion,
            "child",
            [start, line, ret],
            [
                new GraphEdge("e1", "start", "out", "line", "in"),
                new GraphEdge("e2", "line", "out", "return", "in")
            ]);
    }
}
