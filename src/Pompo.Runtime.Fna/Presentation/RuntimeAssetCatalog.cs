using System.Text.Json;
using Pompo.Core;
using Pompo.Core.Project;

namespace Pompo.Runtime.Fna.Presentation;

public sealed record RuntimeAssetCatalog(
    string ContentRoot,
    IReadOnlyDictionary<string, string> AssetPaths,
    IReadOnlyDictionary<string, string> CharacterExpressionAssets)
{
    public string? ResolveAssetPath(string? assetId)
    {
        if (string.IsNullOrWhiteSpace(assetId) ||
            !AssetPaths.TryGetValue(assetId, out var path))
        {
            return null;
        }

        return File.Exists(path) ? path : null;
    }

    public string? ResolveCharacterSpritePath(string characterId, string expressionId)
    {
        return CharacterExpressionAssets.TryGetValue(CreateCharacterExpressionKey(characterId, expressionId), out var assetId)
            ? ResolveAssetPath(assetId)
            : null;
    }

    public static string CreateCharacterExpressionKey(string characterId, string expressionId)
    {
        return $"{characterId}:{expressionId}";
    }
}

public sealed class RuntimeAssetCatalogLoader
{
    public async Task<RuntimeAssetCatalog?> TryLoadFromIrPathAsync(
        string irPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(irPath);

        var dataDirectory = Path.GetDirectoryName(Path.GetFullPath(irPath));
        if (dataDirectory is null)
        {
            return null;
        }

        var projectPath = Path.Combine(dataDirectory, ProjectConstants.ProjectFileName);
        if (!File.Exists(projectPath))
        {
            return null;
        }

        var contentRoot = Directory.GetParent(dataDirectory)?.FullName ?? dataDirectory;
        await using var stream = File.OpenRead(projectPath);
        var project = await JsonSerializer.DeserializeAsync<PompoProjectDocument>(
                stream,
                ProjectFileService.CreateJsonOptions(),
                cancellationToken)
            .ConfigureAwait(false);

        return project is null ? null : Create(contentRoot, project);
    }

    public RuntimeAssetCatalog Create(string contentRoot, PompoProjectDocument project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);

        var assetPaths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var asset in project.Assets.Assets)
        {
            if (string.IsNullOrWhiteSpace(asset.AssetId) || assetPaths.ContainsKey(asset.AssetId))
            {
                continue;
            }

            assetPaths[asset.AssetId] = Path.GetFullPath(Path.Combine(contentRoot, asset.SourcePath));
        }

        var characterSprites = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var character in project.Characters)
        {
            foreach (var expression in character.Expressions)
            {
                if (string.IsNullOrWhiteSpace(character.CharacterId) ||
                    string.IsNullOrWhiteSpace(expression.ExpressionId) ||
                    string.IsNullOrWhiteSpace(expression.Sprite.AssetId))
                {
                    continue;
                }

                var key = RuntimeAssetCatalog.CreateCharacterExpressionKey(character.CharacterId, expression.ExpressionId);
                characterSprites.TryAdd(key, expression.Sprite.AssetId);
            }
        }

        return new RuntimeAssetCatalog(contentRoot, assetPaths, characterSprites);
    }
}
