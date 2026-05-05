using Pompo.Build;
using Pompo.Core.Graphs;
using Pompo.Core.Project;
using Pompo.VisualScripting;
using Pompo.VisualScripting.Runtime;

namespace Pompo.Tests;

public sealed class ProjectTemplateServiceTests
{
    [Fact]
    public async Task CreateSampleVisualNovelAsync_CreatesValidatedMultiSceneProject()
    {
        var root = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        var project = await new ProjectTemplateService().CreateSampleVisualNovelAsync(root, "Sample");
        var graphValidator = new GraphValidator();

        Assert.Equal(3, project.Scenes.Count);
        Assert.Equal(2, project.Characters.Count);
        Assert.Equal(7, project.Assets.Assets.Count);
        Assert.Equal(4, project.Graphs.Count);
        var uiStrings = Assert.Single(project.StringTables, table => table.TableId == "ui");
        Assert.Contains(uiStrings.Entries, entry =>
            entry.Key == "menu.start" &&
            entry.Values["ko"] == "시작" &&
            entry.Values["en"] == "Start");
        Assert.All(uiStrings.Entries, entry =>
        {
            Assert.Contains("ko", entry.Values.Keys);
            Assert.Contains("en", entry.Values.Keys);
        });
        Assert.Contains(project.Graphs.SelectMany(graph => graph.Nodes), node => node.Kind == GraphNodeKind.Choice);
        Assert.Contains(project.Graphs.SelectMany(graph => graph.Nodes), node => node.Kind == GraphNodeKind.Branch);
        Assert.Contains(project.Graphs.SelectMany(graph => graph.Nodes), node => node.Kind == GraphNodeKind.SavePoint);
        Assert.Contains(project.Graphs.SelectMany(graph => graph.Nodes), node => node.Kind == GraphNodeKind.ChangeBackground);
        Assert.Contains(project.Graphs.SelectMany(graph => graph.Nodes), node => node.Kind == GraphNodeKind.ShowCharacter);
        Assert.Contains(project.Graphs.SelectMany(graph => graph.Nodes), node => node.Kind == GraphNodeKind.MoveCharacter);
        Assert.Contains(project.Graphs.SelectMany(graph => graph.Nodes), node => node.Kind == GraphNodeKind.ChangeExpression);
        Assert.Contains(project.Graphs.SelectMany(graph => graph.Nodes), node => node.Kind == GraphNodeKind.HideCharacter);
        Assert.Contains(project.Graphs.SelectMany(graph => graph.Nodes), node => node.Kind == GraphNodeKind.UnlockCg);
        Assert.Contains(project.Graphs.SelectMany(graph => graph.Nodes), node => node.Kind == GraphNodeKind.CallGraph);
        Assert.Contains(project.Graphs.SelectMany(graph => graph.Nodes), node => node.Kind == GraphNodeKind.Return);
        Assert.Contains(project.Graphs.SelectMany(graph => graph.Nodes), node => node.Kind == GraphNodeKind.PlayBgm);
        Assert.Contains(project.Graphs.SelectMany(graph => graph.Nodes), node => node.Kind == GraphNodeKind.PlaySfx);
        Assert.Contains(project.Graphs.SelectMany(graph => graph.Nodes), node => node.Kind == GraphNodeKind.PlayVoice);
        Assert.Contains(project.Graphs.SelectMany(graph => graph.Nodes), node => node.Kind == GraphNodeKind.StopVoice);
        Assert.Contains(project.Graphs.SelectMany(graph => graph.Nodes), node => node.Kind == GraphNodeKind.StopBgm);
        Assert.Contains(project.Characters.Single(character => character.CharacterId == "mina").Expressions, expression => expression.ExpressionId == "smile");
        Assert.Contains(project.Characters.Single(character => character.CharacterId == "mina").Expressions, expression => expression.ExpressionId == "surprised");
        Assert.All(project.Assets.Assets, asset => Assert.True(File.Exists(Path.Combine(root, asset.SourcePath)), asset.SourcePath));
        Assert.True(File.Exists(Path.Combine(root, "Assets", "Audio", "music-theme.wav")));
        Assert.True(File.Exists(Path.Combine(root, "Assets", "Audio", "sfx-chime.wav")));
        Assert.True(File.Exists(Path.Combine(root, "Assets", "Audio", "voice-opening.wav")));

        var introIr = new GraphCompiler().Compile(project.Graphs.Single(graph => graph.GraphId == "graph_intro"));
        var trace = new RuntimeTraceRunner().Run(introIr);
        Assert.Null(trace.Audio.BgmAssetId);
        Assert.Null(trace.Audio.VoiceAssetId);
        Assert.Contains("sfx-chime", trace.Audio.PlayingSfxAssetIds);

        var compiler = new GraphCompiler();
        var graphLibrary = project.Graphs
            .Select(compiler.Compile)
            .ToDictionary(ir => ir.GraphId, StringComparer.Ordinal);
        var cafeTrace = new RuntimeTraceRunner().Run(graphLibrary["graph_cafe"], graphLibrary: graphLibrary);
        var minaState = Assert.Single(cafeTrace.Characters, character => character.CharacterId == "mina");
        Assert.Equal("surprised", minaState.ExpressionId);
        Assert.False(minaState.Visible);
        Assert.Contains("cg-cafe-meeting", cafeTrace.UnlockedCgIds);
        Assert.Contains(cafeTrace.Events, traceEvent => traceEvent.GraphId == "graph_cafe_bonus" && traceEvent.Text == "A short reusable cafe beat returns to the caller.");

        Assert.True(new ProjectValidator().Validate(project, root).IsValid);
        Assert.All(project.Graphs, graph => Assert.True(graphValidator.Validate(graph).IsValid));
        Assert.True(File.Exists(Path.Combine(root, "README.md")));
        var readme = await File.ReadAllTextAsync(Path.Combine(root, "README.md"));
        Assert.Contains("pompo doctor --project .", readme);
        Assert.Contains("pompo validate --project . --json", readme);
        Assert.Contains("pompo asset verify --project . --json", readme);
        Assert.Contains("pompo localization report --project . --json", readme);
        Assert.Contains("pompo build verify", readme);
        Assert.Contains("pompo release package", readme);
        Assert.Contains("pompo release verify", readme);
        Assert.Contains("pompo release audit", readme);
        Assert.Contains("--json", readme);
        Assert.True(File.Exists(Path.Combine(root, "BuildProfiles", "debug.pompo-build.json")));
        Assert.True(File.Exists(Path.Combine(root, "BuildProfiles", "release.pompo-build.json")));
        var releaseProfile = await new BuildProfileFileService().LoadAsync(
            Path.Combine(root, "BuildProfiles", "release.pompo-build.json"));
        Assert.Equal("release", releaseProfile.ProfileName);
        Assert.True(releaseProfile.RunSmokeTest);
        Assert.True(releaseProfile.PackageRuntime);
        Assert.True(releaseProfile.SelfContained);
    }
}
