using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pompo.Core;
using Pompo.Core.Graphs;
using Pompo.Core.Localization;
using Pompo.Core.Project;
using Pompo.VisualScripting;
using Pompo.VisualScripting.Runtime;

namespace Pompo.Tests;

public sealed class RuntimeLocalizationCliTests
{
    [Fact]
    public async Task RuntimeExecutable_PrintsHelp()
    {
        var executable = Path.Combine(
            AppContext.BaseDirectory,
            OperatingSystem.IsWindows() ? "Pompo.Runtime.Fna.exe" : "Pompo.Runtime.Fna");
        var process = Process.Start(new ProcessStartInfo(executable)
        {
            ArgumentList = { "--help" },
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }) ?? throw new InvalidOperationException("Runtime process did not start.");

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, stderr);
        Assert.Contains("--play-ir <path>", stdout, StringComparison.Ordinal);
        Assert.Contains("--run-ir <path>", stdout, StringComparison.Ordinal);
        Assert.Contains("--validate-runtime", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RuntimeExecutable_PrintsVersionJson()
    {
        var executable = Path.Combine(
            AppContext.BaseDirectory,
            OperatingSystem.IsWindows() ? "Pompo.Runtime.Fna.exe" : "Pompo.Runtime.Fna");
        var process = Process.Start(new ProcessStartInfo(executable)
        {
            ArgumentList = { "--version", "--json" },
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }) ?? throw new InvalidOperationException("Runtime process did not start.");

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, stderr);
        using var document = JsonDocument.Parse(stdout);
        Assert.Equal("PompoEngine Runtime", document.RootElement.GetProperty("product").GetString());
        Assert.Equal(ProjectConstants.CurrentSchemaVersion, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("runtimeVersion").GetString()));
    }

    [Fact]
    public async Task PlayIr_ResolvesLocaleFromPackagedProjectData()
    {
        var root = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        var dataRoot = Path.Combine(root, "Data");
        Directory.CreateDirectory(dataRoot);

        var ir = new GraphCompiler().Compile(CreateLocalizedGraph());
        var project = new PompoProjectDocument
        {
            ProjectName = "Localized Runtime",
            StringTables =
            [
                new StringTableDocument(
                    "dialogue",
                    [
                        new StringTableEntry(
                            "line.hello",
                            new Dictionary<string, string> { ["ko"] = "안녕", ["en"] = "Hello" })
                    ])
            ]
        };

        var options = ProjectFileService.CreateJsonOptions();
        var irPath = Path.Combine(dataRoot, "localized.pompo-ir.json");
        await using (var irStream = File.Create(irPath))
        {
            await JsonSerializer.SerializeAsync(irStream, ir, options);
        }

        await using (var projectStream = File.Create(Path.Combine(dataRoot, ProjectConstants.ProjectFileName)))
        {
            await JsonSerializer.SerializeAsync(projectStream, project, options);
        }

        var executable = Path.Combine(
            AppContext.BaseDirectory,
            OperatingSystem.IsWindows() ? "Pompo.Runtime.Fna.exe" : "Pompo.Runtime.Fna");
        var process = Process.Start(new ProcessStartInfo(executable)
        {
            ArgumentList = { "--play-ir", irPath, "--locale", "ko", "--json-trace" },
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }) ?? throw new InvalidOperationException("Runtime process did not start.");

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, stderr);
        var trace = JsonSerializer.Deserialize<RuntimeTraceResult>(stdout, options);
        Assert.NotNull(trace);
        Assert.Contains(trace.Events, traceEvent => traceEvent.Kind == "line" && traceEvent.Text == "안녕");
    }

    [Fact]
    public async Task PlayIr_LoadsSiblingIrFilesForCallGraph()
    {
        var root = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        var dataRoot = Path.Combine(root, "Data");
        Directory.CreateDirectory(dataRoot);

        var compiler = new GraphCompiler();
        var rootIr = compiler.Compile(CreateCallGraphRootGraph());
        var childIr = compiler.Compile(CreateCallGraphChildGraph());
        var options = ProjectFileService.CreateJsonOptions();
        var rootIrPath = Path.Combine(dataRoot, "root.pompo-ir.json");
        await using (var rootStream = File.Create(rootIrPath))
        {
            await JsonSerializer.SerializeAsync(rootStream, rootIr, options);
        }

        await using (var childStream = File.Create(Path.Combine(dataRoot, "child.pompo-ir.json")))
        {
            await JsonSerializer.SerializeAsync(childStream, childIr, options);
        }

        var executable = Path.Combine(
            AppContext.BaseDirectory,
            OperatingSystem.IsWindows() ? "Pompo.Runtime.Fna.exe" : "Pompo.Runtime.Fna");
        var process = Process.Start(new ProcessStartInfo(executable)
        {
            ArgumentList = { "--play-ir", rootIrPath, "--json-trace" },
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }) ?? throw new InvalidOperationException("Runtime process did not start.");

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, stderr);
        var trace = JsonSerializer.Deserialize<RuntimeTraceResult>(stdout, options);
        Assert.NotNull(trace);
        Assert.Contains(trace.Events, traceEvent => traceEvent.Kind == "line" && traceEvent.GraphId == "child" && traceEvent.Text == "Inside child");
        Assert.Contains(trace.Events, traceEvent => traceEvent.Kind == "line" && traceEvent.GraphId == "root" && traceEvent.Text == "Back in root");
    }

    [Fact]
    public async Task PlayIr_ReportsInvalidChoiceSelectionsClearly()
    {
        var root = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        var dataRoot = Path.Combine(root, "Data");
        Directory.CreateDirectory(dataRoot);

        var ir = new GraphCompiler().Compile(GraphFixtures.LinearGraph());
        var options = ProjectFileService.CreateJsonOptions();
        var irPath = Path.Combine(dataRoot, "intro.pompo-ir.json");
        await using (var irStream = File.Create(irPath))
        {
            await JsonSerializer.SerializeAsync(irStream, ir, options);
        }

        var executable = Path.Combine(
            AppContext.BaseDirectory,
            OperatingSystem.IsWindows() ? "Pompo.Runtime.Fna.exe" : "Pompo.Runtime.Fna");
        var process = Process.Start(new ProcessStartInfo(executable)
        {
            ArgumentList = { "--play-ir", irPath, "--choices", "abc" },
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }) ?? throw new InvalidOperationException("Runtime process did not start.");

        await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.NotEqual(0, process.ExitCode);
        Assert.DoesNotContain("Unhandled exception", stderr, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--choices value 'abc' is invalid", stderr, StringComparison.Ordinal);
    }

    private static GraphDocument CreateLocalizedGraph()
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
            1,
            "localized",
            [start, line, end],
            [
                new GraphEdge("e1", "start", "out", "line", "in"),
                new GraphEdge("e2", "line", "out", "end", "in")
            ]);
    }

    private static GraphDocument CreateCallGraphRootGraph()
    {
        var start = new GraphNode(
            "start",
            GraphNodeKind.Start,
            new GraphPoint(0, 0),
            [NodeCatalog.OutExecPort()],
            []);
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
            new JsonObject { ["text"] = "Back in root" });
        var end = new GraphNode(
            "end",
            GraphNodeKind.EndScene,
            new GraphPoint(600, 0),
            [NodeCatalog.InExecPort()],
            []);

        return new GraphDocument(
            1,
            "root",
            [start, call, line, end],
            [
                new GraphEdge("e1", "start", "out", "call_child", "in"),
                new GraphEdge("e2", "call_child", "out", "line", "in"),
                new GraphEdge("e3", "line", "out", "end", "in")
            ]);
    }

    private static GraphDocument CreateCallGraphChildGraph()
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
            new JsonObject { ["text"] = "Inside child" });
        var ret = new GraphNode(
            "return",
            GraphNodeKind.Return,
            new GraphPoint(400, 0),
            [NodeCatalog.InExecPort()],
            []);

        return new GraphDocument(
            1,
            "child",
            [start, line, ret],
            [
                new GraphEdge("e1", "start", "out", "line", "in"),
                new GraphEdge("e2", "line", "out", "return", "in")
            ]);
    }
}
