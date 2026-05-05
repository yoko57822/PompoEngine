using Pompo.Core.Assets;
using Pompo.Core.Runtime;

namespace Pompo.Core.Scenes;

public sealed record SceneDefinition(
    string SceneId,
    string DisplayName,
    IReadOnlyList<SceneLayer> Layers,
    IReadOnlyList<SceneCharacterPlacement> Characters,
    string StartGraphId);

public sealed record SceneLayer(
    string LayerId,
    RuntimeLayer Layer,
    PompoAssetRef? Asset,
    float X = 0,
    float Y = 0,
    float Opacity = 1);

public sealed record SceneCharacterPlacement(
    string PlacementId,
    string CharacterId,
    RuntimeLayer Layer,
    float X,
    float Y,
    string? InitialExpressionId = null);
