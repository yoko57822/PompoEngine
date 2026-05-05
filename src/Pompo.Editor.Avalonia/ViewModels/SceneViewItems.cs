using Pompo.Core.Runtime;

namespace Pompo.Editor.Avalonia.ViewModels;

public sealed record SceneViewItem(
    string SceneId,
    string DisplayName,
    string StartGraphId,
    string BackgroundAssetId,
    int CharacterCount,
    bool IsSelected);

public sealed record SceneLayerViewItem(
    string LayerId,
    RuntimeLayer Layer,
    string AssetId,
    float X,
    float Y,
    float Opacity);

public sealed record SceneCharacterPlacementViewItem(
    string PlacementId,
    string CharacterId,
    RuntimeLayer Layer,
    float X,
    float Y,
    string InitialExpressionId,
    bool IsSelected);
