namespace Pompo.Core.Assets;

public enum PompoAssetType
{
    Image,
    Audio,
    Font,
    Script,
    Data
}

public sealed record PompoAssetRef(string AssetId, PompoAssetType Type);

public sealed record AssetImportOptions(
    bool PreserveOriginal = true,
    string? Locale = null,
    int? PixelsPerUnit = null,
    bool Loop = false);

public sealed record AssetUsage(string DocumentPath, string Kind, string? ElementId = null);

public sealed record AssetMetadata(
    string AssetId,
    string SourcePath,
    PompoAssetType Type,
    AssetImportOptions ImportOptions,
    string Hash,
    IReadOnlyList<AssetUsage> Usages);

public sealed class AssetDatabase
{
    public List<AssetMetadata> Assets { get; init; } = [];

    public AssetMetadata? Find(string assetId)
    {
        return Assets.FirstOrDefault(asset => string.Equals(asset.AssetId, assetId, StringComparison.Ordinal));
    }

    public bool Contains(string assetId)
    {
        return Find(assetId) is not null;
    }
}
