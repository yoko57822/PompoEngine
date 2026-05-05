using Pompo.Core.Assets;
using Pompo.Core.Characters;
using Pompo.Core.Project;
using Pompo.Runtime.Fna.Presentation;

namespace Pompo.Tests;

public sealed class RuntimeAssetCatalogTests
{
    [Fact]
    public async Task TryLoadFromIrPathAsync_ResolvesBuildOutputAssetPathsAndCharacterSprites()
    {
        var root = CreateTempDirectory();
        var data = Path.Combine(root, "Data");
        var image = Path.Combine(root, "Assets", "Images", "mina.png");
        var audio = Path.Combine(root, "Assets", "Audio", "theme.wav");
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(Path.GetDirectoryName(image)!);
        Directory.CreateDirectory(Path.GetDirectoryName(audio)!);
        await File.WriteAllTextAsync(image, "sprite");
        await File.WriteAllTextAsync(audio, "audio");
        await File.WriteAllTextAsync(Path.Combine(data, "graph_intro.pompo-ir.json"), "{}");
        await new ProjectFileService().SaveAsync(
            data,
            new PompoProjectDocument
            {
                ProjectName = "Runtime Assets",
                Assets = new AssetDatabase
                {
                    Assets =
                    [
                        new AssetMetadata(
                            "mina-sprite",
                            "Assets/Images/mina.png",
                            PompoAssetType.Image,
                            new AssetImportOptions(),
                            "hash",
                            []),
                        new AssetMetadata(
                            "theme",
                            "Assets/Audio/theme.wav",
                            PompoAssetType.Audio,
                            new AssetImportOptions(),
                            "hash",
                            [])
                    ]
                },
                Characters =
                [
                    new CharacterDefinition(
                        "mina",
                        "Mina",
                        "smile",
                        [new CharacterExpression("smile", new PompoAssetRef("mina-sprite", PompoAssetType.Image))])
                ]
            });

        var catalog = await new RuntimeAssetCatalogLoader().TryLoadFromIrPathAsync(
            Path.Combine(data, "graph_intro.pompo-ir.json"));

        Assert.NotNull(catalog);
        Assert.Equal(image, catalog.ResolveAssetPath("mina-sprite"));
        Assert.Equal(audio, catalog.ResolveAssetPath("theme"));
        Assert.Equal(image, catalog.ResolveCharacterSpritePath("mina", "smile"));
    }

    [Fact]
    public async Task TryLoadFromIrPathAsync_ReturnsNullWhenProjectFileIsMissing()
    {
        var root = CreateTempDirectory();
        var ir = Path.Combine(root, "Data", "graph.pompo-ir.json");
        Directory.CreateDirectory(Path.GetDirectoryName(ir)!);
        await File.WriteAllTextAsync(ir, "{}");

        var catalog = await new RuntimeAssetCatalogLoader().TryLoadFromIrPathAsync(ir);

        Assert.Null(catalog);
    }

    [Fact]
    public void Create_IgnoresDuplicateAndEmptyCatalogKeys()
    {
        var root = CreateTempDirectory();
        var firstSprite = Path.Combine(root, "Assets", "Images", "mina-1.png");
        var secondSprite = Path.Combine(root, "Assets", "Images", "mina-2.png");
        Directory.CreateDirectory(Path.GetDirectoryName(firstSprite)!);
        File.WriteAllText(firstSprite, "sprite-1");
        File.WriteAllText(secondSprite, "sprite-2");
        var project = new PompoProjectDocument
        {
            ProjectName = "Duplicate Runtime Assets",
            Assets = new AssetDatabase
            {
                Assets =
                [
                    new AssetMetadata("mina-sprite", "Assets/Images/mina-1.png", PompoAssetType.Image, new AssetImportOptions(), "hash", []),
                    new AssetMetadata("mina-sprite", "Assets/Images/mina-2.png", PompoAssetType.Image, new AssetImportOptions(), "hash", []),
                    new AssetMetadata("", "Assets/Images/empty.png", PompoAssetType.Image, new AssetImportOptions(), "hash", [])
                ]
            },
            Characters =
            [
                new CharacterDefinition(
                    "mina",
                    "Mina",
                    "smile",
                    [
                        new CharacterExpression("smile", new PompoAssetRef("mina-sprite", PompoAssetType.Image)),
                        new CharacterExpression("smile", new PompoAssetRef("other-sprite", PompoAssetType.Image)),
                        new CharacterExpression("", new PompoAssetRef("empty-expression", PompoAssetType.Image))
                    ])
            ]
        };

        var catalog = new RuntimeAssetCatalogLoader().Create(root, project);

        Assert.Equal(firstSprite, catalog.ResolveAssetPath("mina-sprite"));
        Assert.Equal(firstSprite, catalog.ResolveCharacterSpritePath("mina", "smile"));
        Assert.Null(catalog.ResolveCharacterSpritePath("mina", ""));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
