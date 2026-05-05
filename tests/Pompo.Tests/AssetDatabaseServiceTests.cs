using Pompo.Core.Assets;
using Pompo.Core.Project;

namespace Pompo.Tests;

public sealed class AssetDatabaseServiceTests
{
    [Fact]
    public async Task ImportAsync_CopiesFileAndRegistersHash()
    {
        var root = CreateTempDirectory();
        var source = Path.Combine(root, "source.png");
        await File.WriteAllTextAsync(source, "image-bytes");
        var project = new PompoProjectDocument { ProjectName = "Assets" };

        var metadata = await new AssetDatabaseService().ImportAsync(
            root,
            project,
            new AssetImportRequest(source, PompoAssetType.Image, "bg-main"));

        Assert.Equal("bg-main", metadata.AssetId);
        Assert.Equal("Assets/Images/bg-main.png", metadata.SourcePath);
        Assert.Single(project.Assets.Assets);
        Assert.True(File.Exists(Path.Combine(root, metadata.SourcePath)));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(Path.Combine(root, metadata.SourcePath))!, "*.tmp"));
        Assert.Equal(
            await AssetDatabaseService.ComputeSha256Async(Path.Combine(root, metadata.SourcePath)),
            metadata.Hash);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("bad/id")]
    [InlineData("bad id")]
    [InlineData(".hidden")]
    [InlineData("asset.")]
    public async Task ImportAsync_RejectsUnsafeAssetId(string assetId)
    {
        var root = CreateTempDirectory();
        var source = Path.Combine(root, "source.png");
        await File.WriteAllTextAsync(source, "image-bytes");
        var project = new PompoProjectDocument { ProjectName = "Assets" };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            new AssetDatabaseService().ImportAsync(
                root,
                project,
                new AssetImportRequest(source, PompoAssetType.Image, assetId)));

        Assert.Contains("Asset ID", exception.Message, StringComparison.Ordinal);
        Assert.Empty(project.Assets.Assets);
        Assert.False(File.Exists(Path.Combine(root, "outside.png")));
    }

    [Fact]
    public async Task ValidateHashesAsync_ReportsChangedAsset()
    {
        var root = CreateTempDirectory();
        var source = Path.Combine(root, "source.txt");
        await File.WriteAllTextAsync(source, "v1");
        var project = new PompoProjectDocument { ProjectName = "Assets" };
        var metadata = await new AssetDatabaseService().ImportAsync(
            root,
            project,
            new AssetImportRequest(source, PompoAssetType.Data, "script-data"));
        await File.WriteAllTextAsync(Path.Combine(root, metadata.SourcePath), "v2");

        var diagnostics = await new AssetDatabaseService().ValidateHashesAsync(root, project);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "ASSET002");
    }

    [Fact]
    public async Task Delete_RemovesUnusedAssetAndFile()
    {
        var root = CreateTempDirectory();
        var source = Path.Combine(root, "source.png");
        await File.WriteAllTextAsync(source, "image-bytes");
        var project = new PompoProjectDocument { ProjectName = "Assets" };
        var metadata = await new AssetDatabaseService().ImportAsync(
            root,
            project,
            new AssetImportRequest(source, PompoAssetType.Image, "unused-image"));
        var importedPath = Path.Combine(root, metadata.SourcePath);

        new AssetDatabaseService().Delete(root, project, metadata.AssetId);

        Assert.Null(project.Assets.Find(metadata.AssetId));
        Assert.False(File.Exists(importedPath));
    }

    [Fact]
    public async Task Delete_RejectsReferencedAsset()
    {
        var root = CreateTempDirectory();
        var project = await new ProjectTemplateService()
            .CreateSampleVisualNovelAsync(root, "ReferencedAsset");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new AssetDatabaseService().Delete(root, project, "bg-intro"));

        Assert.Contains("referenced by", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(project.Assets.Find("bg-intro"));
        Assert.True(File.Exists(Path.Combine(root, "Assets/Images/bg-intro.png")));
    }

    [Fact]
    public async Task CountReferences_UsesSceneCharacterAndGraphReferences()
    {
        var root = CreateTempDirectory();
        var project = await new ProjectTemplateService()
            .CreateSampleVisualNovelAsync(root, "ReferenceCounts");

        Assert.True(AssetDatabaseService.CountReferences(project, "bg-intro") >= 2);
        Assert.True(AssetDatabaseService.CountReferences(project, "mina-smile") >= 2);
        Assert.Equal(0, AssetDatabaseService.CountReferences(project, "missing-asset"));
    }

    [Fact]
    public void CountReferences_UsesRuntimeUiSkinReferences()
    {
        var project = new PompoProjectDocument
        {
            ProjectName = "Skin References",
            RuntimeUiSkin = new PompoRuntimeUiSkin(
                DialogueBox: new PompoAssetRef("ui-dialogue", PompoAssetType.Image),
                ChoiceSelectedBox: new PompoAssetRef("ui-choice-selected", PompoAssetType.Image),
                SaveSlotSelected: new PompoAssetRef("ui-choice-selected", PompoAssetType.Image),
                ChoiceDisabledBox: new PompoAssetRef("ui-choice-disabled", PompoAssetType.Image))
        };

        Assert.Equal(1, AssetDatabaseService.CountReferences(project, "ui-dialogue"));
        Assert.Equal(2, AssetDatabaseService.CountReferences(project, "ui-choice-selected"));
        Assert.Equal(1, AssetDatabaseService.CountReferences(project, "ui-choice-disabled"));
        Assert.Equal("runtime UI skin 'DialogueBox'", AssetDatabaseService.FindAssetReference(project, "ui-dialogue"));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
