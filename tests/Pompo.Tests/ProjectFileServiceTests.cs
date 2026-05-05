using Pompo.Core;
using Pompo.Core.Project;

namespace Pompo.Tests;

public sealed class ProjectFileServiceTests
{
    [Fact]
    public async Task CreateAsync_WritesProjectFileAndRequiredFolders()
    {
        var root = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        var service = new ProjectFileService();

        var created = await service.CreateAsync(root, "Test VN");
        var loaded = await service.LoadAsync(root);

        Assert.Equal(created.ProjectId, loaded.ProjectId);
        Assert.Equal("Test VN", loaded.ProjectName);
        Assert.True(File.Exists(Path.Combine(root, ProjectConstants.ProjectFileName)));
        foreach (var folder in ProjectConstants.RequiredFolders)
        {
            Assert.True(Directory.Exists(Path.Combine(root, folder)), folder);
        }
    }

    [Fact]
    public async Task LoadAsync_MigratesLegacySchemaToCurrentDefaults()
    {
        var root = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, ProjectConstants.ProjectFileName);
        await File.WriteAllTextAsync(
            path,
            """
            {
              "schemaVersion": 0,
              "projectName": "Legacy",
              "virtualWidth": 0,
              "virtualHeight": 0,
              "supportedLocales": []
            }
            """);

        var loaded = await new ProjectFileService().LoadAsync(root);

        Assert.Equal(ProjectConstants.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal("Legacy", loaded.ProjectName);
        Assert.Equal(1920, loaded.VirtualWidth);
        Assert.Equal(1080, loaded.VirtualHeight);
        Assert.Equal(["ko", "en"], loaded.SupportedLocales);
        Assert.Equal(new PompoRuntimeUiTheme().DialogueBackground, loaded.RuntimeUiTheme.DialogueBackground);
        Assert.NotNull(loaded.RuntimeUiSkin);
        Assert.NotNull(loaded.RuntimeUiAnimation);
        Assert.NotNull(loaded.RuntimePlayback);
    }

    [Fact]
    public async Task LoadAsync_MigratesSchemaOneToRuntimeUiThemeSchema()
    {
        var root = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            Path.Combine(root, ProjectConstants.ProjectFileName),
            """
            {
              "schemaVersion": 1,
              "projectName": "Schema One",
              "supportedLocales": ["ko", "en"]
            }
            """);

        var loaded = await new ProjectFileService().LoadAsync(root);

        Assert.Equal(ProjectConstants.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal("Schema One", loaded.ProjectName);
        Assert.Equal(new PompoRuntimeUiTheme().CanvasClear, loaded.RuntimeUiTheme.CanvasClear);
        Assert.NotNull(loaded.RuntimeUiSkin);
    }

    [Fact]
    public async Task LoadAsync_MigratesSchemaTwoToRuntimeUiSkinSchema()
    {
        var root = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            Path.Combine(root, ProjectConstants.ProjectFileName),
            """
            {
              "schemaVersion": 2,
              "projectName": "Schema Two",
              "supportedLocales": ["ko", "en"],
              "runtimeUiTheme": {
                "canvasClear": "#010203"
              }
            }
            """);

        var loaded = await new ProjectFileService().LoadAsync(root);

        Assert.Equal(ProjectConstants.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal("Schema Two", loaded.ProjectName);
        Assert.Equal("#010203", loaded.RuntimeUiTheme.CanvasClear);
        Assert.NotNull(loaded.RuntimeUiSkin);
        Assert.NotNull(loaded.RuntimeUiLayout);
    }

    [Fact]
    public async Task LoadAsync_MigratesSchemaThreeToRuntimeUiLayoutSchema()
    {
        var root = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            Path.Combine(root, ProjectConstants.ProjectFileName),
            """
            {
              "schemaVersion": 3,
              "projectName": "Schema Three",
              "supportedLocales": ["ko", "en"],
              "runtimeUiSkin": {
                "dialogueBox": null
              }
            }
            """);

        var loaded = await new ProjectFileService().LoadAsync(root);

        Assert.Equal(ProjectConstants.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal("Schema Three", loaded.ProjectName);
        Assert.Equal(new PompoRuntimeUiLayoutSettings().DialogueTextBox, loaded.RuntimeUiLayout.DialogueTextBox);
        Assert.NotNull(loaded.RuntimeUiAnimation);
    }

    [Fact]
    public async Task LoadAsync_MigratesSchemaFourToRuntimeUiAnimationSchema()
    {
        var root = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            Path.Combine(root, ProjectConstants.ProjectFileName),
            """
            {
              "schemaVersion": 4,
              "projectName": "Schema Four",
              "supportedLocales": ["ko", "en"],
              "runtimeUiLayout": {
                "choiceBoxWidth": 640
              }
            }
            """);

        var loaded = await new ProjectFileService().LoadAsync(root);

        Assert.Equal(ProjectConstants.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal("Schema Four", loaded.ProjectName);
        Assert.Equal(640, loaded.RuntimeUiLayout.ChoiceBoxWidth);
        Assert.Equal(new PompoRuntimeUiAnimationSettings().PanelFadeMilliseconds, loaded.RuntimeUiAnimation.PanelFadeMilliseconds);
    }

    [Fact]
    public async Task LoadAsync_MigratesSchemaFiveToRuntimePlaybackSchema()
    {
        var root = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            Path.Combine(root, ProjectConstants.ProjectFileName),
            """
            {
              "schemaVersion": 5,
              "projectName": "Schema Five",
              "supportedLocales": ["ko", "en"],
              "runtimeUiAnimation": {
                "textRevealCharactersPerSecond": 60
              }
            }
            """);

        var loaded = await new ProjectFileService().LoadAsync(root);

        Assert.Equal(ProjectConstants.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal("Schema Five", loaded.ProjectName);
        Assert.Equal(60, loaded.RuntimeUiAnimation.TextRevealCharactersPerSecond);
        Assert.Equal(new PompoRuntimePlaybackSettings().AutoForwardDelayMilliseconds, loaded.RuntimePlayback.AutoForwardDelayMilliseconds);
    }

    [Fact]
    public async Task LoadAsync_MigratesSchemaSixToDisabledChoiceSkinSchema()
    {
        var root = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            Path.Combine(root, ProjectConstants.ProjectFileName),
            """
            {
              "schemaVersion": 6,
              "projectName": "Schema Six",
              "supportedLocales": ["ko", "en"],
              "runtimeUiSkin": {
                "choiceSelectedBox": {
                  "assetId": "ui-choice-selected",
                  "type": "Image"
                }
              }
            }
            """);

        var loaded = await new ProjectFileService().LoadAsync(root);

        Assert.Equal(ProjectConstants.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal("Schema Six", loaded.ProjectName);
        Assert.Equal("ui-choice-selected", loaded.RuntimeUiSkin.ChoiceSelectedBox?.AssetId);
        Assert.Null(loaded.RuntimeUiSkin.ChoiceDisabledBox);
    }

    [Fact]
    public async Task LoadAsync_NormalizesNullRuntimeUiAppearance()
    {
        var root = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            Path.Combine(root, ProjectConstants.ProjectFileName),
            """
            {
              "schemaVersion": 3,
              "projectName": "Null Appearance",
              "runtimeUiTheme": null,
              "runtimeUiSkin": null,
              "runtimeUiAnimation": null,
              "runtimePlayback": null
            }
            """);

        var loaded = await new ProjectFileService().LoadAsync(root);

        Assert.Equal(ProjectConstants.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.NotNull(loaded.RuntimeUiTheme);
        Assert.NotNull(loaded.RuntimeUiSkin);
        Assert.NotNull(loaded.RuntimeUiLayout);
        Assert.NotNull(loaded.RuntimeUiAnimation);
        Assert.NotNull(loaded.RuntimePlayback);
    }

    [Fact]
    public async Task LoadAsync_RejectsFutureSchema()
    {
        var root = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            Path.Combine(root, ProjectConstants.ProjectFileName),
            """
            {
              "schemaVersion": 999,
              "projectName": "Future"
            }
            """);

        await Assert.ThrowsAsync<InvalidDataException>(() => new ProjectFileService().LoadAsync(root));
    }

    [Fact]
    public async Task AtomicFileWriter_ReplacesExistingJsonWithoutLeavingTempFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "atomic.json");
        await File.WriteAllTextAsync(path, """{"projectName":"Old"}""");

        await AtomicFileWriter.WriteJsonAsync(
            path,
            new PompoProjectDocument { ProjectName = "New" },
            ProjectFileService.CreateJsonOptions());

        var contents = await File.ReadAllTextAsync(path);
        Assert.Contains("\"projectName\": \"New\"", contents);
        Assert.Empty(Directory.EnumerateFiles(root, "*.tmp"));
    }

    [Fact]
    public async Task AtomicFileWriter_CopiesFileWithoutLeavingTempFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "source.txt");
        var target = Path.Combine(root, "Assets", "Images", "target.txt");
        await File.WriteAllTextAsync(source, "asset-bytes");

        await AtomicFileWriter.CopyFileAsync(source, target);

        Assert.Equal("asset-bytes", await File.ReadAllTextAsync(target));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(target)!, "*.tmp"));
    }
}
