using System.Security.Cryptography;
using System.Text.Json;
using Pompo.Core.Graphs;
using Pompo.Core.Project;

namespace Pompo.Core.Assets;

public sealed record AssetImportRequest(
    string SourceFile,
    PompoAssetType Type,
    string? AssetId = null,
    AssetImportOptions? Options = null);

public sealed class AssetDatabaseService
{
    public async Task<AssetMetadata> ImportAsync(
        string projectRoot,
        PompoProjectDocument project,
        AssetImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceFile);

        if (!File.Exists(request.SourceFile))
        {
            throw new FileNotFoundException("Asset source file does not exist.", request.SourceFile);
        }

        var assetId = request.AssetId ?? CreateAssetId(request.SourceFile);
        ValidateAssetId(assetId);
        if (project.Assets.Contains(assetId))
        {
            throw new InvalidOperationException($"Asset '{assetId}' already exists.");
        }

        var relativePath = GetAssetTargetPath(request.Type, assetId, Path.GetExtension(request.SourceFile));
        var targetPath = Path.Combine(projectRoot, relativePath);
        EnsurePathIsInsideProject(projectRoot, targetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        await AtomicFileWriter.CopyFileAsync(request.SourceFile, targetPath, cancellationToken)
            .ConfigureAwait(false);

        var metadata = new AssetMetadata(
            assetId,
            relativePath.Replace('\\', '/'),
            request.Type,
            request.Options ?? new AssetImportOptions(),
            await ComputeSha256Async(targetPath, cancellationToken).ConfigureAwait(false),
            []);

        project.Assets.Assets.Add(metadata);
        return metadata;
    }

    public async Task<IReadOnlyList<ProjectDiagnostic>> ValidateHashesAsync(
        string projectRoot,
        PompoProjectDocument project,
        CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<ProjectDiagnostic>();
        foreach (var asset in project.Assets.Assets)
        {
            var path = Path.Combine(projectRoot, asset.SourcePath);
            if (!File.Exists(path))
            {
                diagnostics.Add(new ProjectDiagnostic(
                    "ASSET001",
                    $"Asset file '{asset.SourcePath}' does not exist.",
                    asset.SourcePath,
                    asset.AssetId));
                continue;
            }

            var actualHash = await ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(actualHash, asset.Hash, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new ProjectDiagnostic(
                    "ASSET002",
                    $"Asset '{asset.AssetId}' hash mismatch. Expected '{asset.Hash}', actual '{actualHash}'.",
                    asset.SourcePath,
                    asset.AssetId));
            }
        }

        return diagnostics;
    }

    public async Task<int> RefreshHashesAsync(
        string projectRoot,
        PompoProjectDocument project,
        CancellationToken cancellationToken = default)
    {
        var refreshed = 0;
        for (var index = 0; index < project.Assets.Assets.Count; index++)
        {
            var asset = project.Assets.Assets[index];
            var path = Path.Combine(projectRoot, asset.SourcePath);
            if (!File.Exists(path))
            {
                continue;
            }

            var hash = await ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);
            if (string.Equals(hash, asset.Hash, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            project.Assets.Assets[index] = asset with { Hash = hash };
            refreshed++;
        }

        return refreshed;
    }

    public void Delete(
        string projectRoot,
        PompoProjectDocument project,
        string assetId,
        bool deleteFile = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        var asset = project.Assets.Find(assetId)
            ?? throw new InvalidOperationException($"Asset '{assetId}' does not exist.");
        var reference = FindAssetReference(project, assetId);
        if (reference is not null)
        {
            throw new InvalidOperationException($"Asset '{assetId}' is referenced by {reference}.");
        }

        project.Assets.Assets.RemoveAll(existing => string.Equals(existing.AssetId, assetId, StringComparison.Ordinal));
        if (deleteFile)
        {
            DeleteProjectAssetFile(projectRoot, asset.SourcePath);
        }
    }

    public static int CountReferences(PompoProjectDocument project, string assetId)
    {
        var count = 0;
        count += project.Scenes.Sum(scene => scene.Layers.Count(layer =>
            string.Equals(layer.Asset?.AssetId, assetId, StringComparison.Ordinal)));
        count += project.Characters.Sum(character => character.Expressions.Count(expression =>
            string.Equals(expression.Sprite.AssetId, assetId, StringComparison.Ordinal)));
        count += project.Graphs.Sum(graph => graph.Nodes.Count(node => NodeReferencesAsset(node, assetId)));
        count += CountRuntimeUiSkinReferences(project.RuntimeUiSkin ?? new PompoRuntimeUiSkin(), assetId);
        return count;
    }

    public static string? FindAssetReference(PompoProjectDocument project, string assetId)
    {
        foreach (var scene in project.Scenes)
        {
            foreach (var layer in scene.Layers)
            {
                if (string.Equals(layer.Asset?.AssetId, assetId, StringComparison.Ordinal))
                {
                    return $"scene '{scene.SceneId}' layer '{layer.LayerId}'";
                }
            }
        }

        foreach (var character in project.Characters)
        {
            foreach (var expression in character.Expressions)
            {
                if (string.Equals(expression.Sprite.AssetId, assetId, StringComparison.Ordinal))
                {
                    return $"character '{character.CharacterId}' expression '{expression.ExpressionId}'";
                }
            }
        }

        foreach (var graph in project.Graphs)
        {
            foreach (var node in graph.Nodes)
            {
                if (NodeReferencesAsset(node, assetId))
                {
                    return $"graph '{graph.GraphId}' node '{node.NodeId}'";
                }
            }
        }

        foreach (var (slotName, assetRef) in GetRuntimeUiSkinSlots(project.RuntimeUiSkin ?? new PompoRuntimeUiSkin()))
        {
            if (string.Equals(assetRef?.AssetId, assetId, StringComparison.Ordinal))
            {
                return $"runtime UI skin '{slotName}'";
            }
        }

        return null;
    }

    public static async Task<string> ComputeSha256Async(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static void ValidateAssetId(string assetId)
    {
        if (!IsValidAssetId(assetId))
        {
            throw new ArgumentException("Asset ID can use letters, numbers, '-', '_', or '.', and must not start or end with '.'.", nameof(assetId));
        }
    }

    public static bool IsValidAssetId(string assetId)
    {
        if (string.IsNullOrWhiteSpace(assetId) ||
            !string.Equals(assetId, assetId.Trim(), StringComparison.Ordinal) ||
            assetId is "." or ".." ||
            assetId.StartsWith(".", StringComparison.Ordinal) ||
            assetId.EndsWith(".", StringComparison.Ordinal))
        {
            return false;
        }

        return assetId.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');
    }

    public static bool IsSafeProjectRelativePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized) ||
            Path.IsPathRooted(normalized) ||
            normalized.StartsWith("/", StringComparison.Ordinal) ||
            normalized.Contains("../", StringComparison.Ordinal) ||
            normalized.Contains("/..", StringComparison.Ordinal))
        {
            return false;
        }

        return !normalized.Equals("..", StringComparison.Ordinal);
    }

    private static string CreateAssetId(string sourceFile)
    {
        var name = Path.GetFileNameWithoutExtension(sourceFile)
            .Trim()
            .ToLowerInvariant();
        var sanitized = new string(name.Select(character =>
            char.IsLetterOrDigit(character) ? character : '-').ToArray());
        sanitized = string.Join('-', sanitized.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(sanitized) ? Guid.NewGuid().ToString("N") : sanitized;
    }

    private static string GetAssetTargetPath(PompoAssetType type, string assetId, string extension)
    {
        var folder = type switch
        {
            PompoAssetType.Image => "Assets/Images",
            PompoAssetType.Audio => "Assets/Audio",
            PompoAssetType.Font => "Assets/Fonts",
            PompoAssetType.Script => "Scripts",
            PompoAssetType.Data => "Assets/Data",
            _ => "Assets/Data"
        };

        return Path.Combine(folder, $"{assetId}{extension}");
    }

    private static bool NodeReferencesAsset(GraphNode node, string assetId)
    {
        return NodeStringPropertyEquals(node, "assetId", assetId) ||
            NodeStringPropertyEquals(node, "backgroundAssetId", assetId) ||
            NodeStringPropertyEquals(node, "bgmAssetId", assetId) ||
            NodeStringPropertyEquals(node, "sfxAssetId", assetId);
    }

    private static int CountRuntimeUiSkinReferences(PompoRuntimeUiSkin skin, string assetId)
    {
        return GetRuntimeUiSkinSlots(skin)
            .Count(slot => string.Equals(slot.AssetRef?.AssetId, assetId, StringComparison.Ordinal));
    }

    private static IEnumerable<(string SlotName, PompoAssetRef? AssetRef)> GetRuntimeUiSkinSlots(PompoRuntimeUiSkin skin)
    {
        yield return (nameof(skin.DialogueBox), skin.DialogueBox);
        yield return (nameof(skin.NameBox), skin.NameBox);
        yield return (nameof(skin.ChoiceBox), skin.ChoiceBox);
        yield return (nameof(skin.ChoiceSelectedBox), skin.ChoiceSelectedBox);
        yield return (nameof(skin.ChoiceDisabledBox), skin.ChoiceDisabledBox);
        yield return (nameof(skin.SaveMenuPanel), skin.SaveMenuPanel);
        yield return (nameof(skin.SaveSlot), skin.SaveSlot);
        yield return (nameof(skin.SaveSlotSelected), skin.SaveSlotSelected);
        yield return (nameof(skin.SaveSlotEmpty), skin.SaveSlotEmpty);
        yield return (nameof(skin.BacklogPanel), skin.BacklogPanel);
    }

    private static bool NodeStringPropertyEquals(GraphNode node, string key, string value)
    {
        return node.Properties.TryGetPropertyValue(key, out var property) &&
            property is not null &&
            property.GetValueKind() == JsonValueKind.String &&
            string.Equals(property.GetValue<string>(), value, StringComparison.Ordinal);
    }

    private static void DeleteProjectAssetFile(string projectRoot, string sourcePath)
    {
        var root = Path.GetFullPath(projectRoot);
        var path = Path.GetFullPath(Path.Combine(root, sourcePath));
        EnsurePathIsInsideProject(root, path);

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void EnsurePathIsInsideProject(string projectRoot, string path)
    {
        var root = Path.GetFullPath(projectRoot);
        var fullPath = Path.GetFullPath(path);
        var relativePath = Path.GetRelativePath(root, fullPath);
        if (relativePath == ".." ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException($"Asset path '{fullPath}' is outside the project root.");
        }
    }
}
