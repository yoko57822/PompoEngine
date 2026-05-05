using Pompo.Core.Assets;

namespace Pompo.Core.Characters;

public sealed record CharacterDefinition(
    string CharacterId,
    string DisplayName,
    string? DefaultExpression,
    IReadOnlyList<CharacterExpression> Expressions);

public sealed record CharacterExpression(
    string ExpressionId,
    PompoAssetRef Sprite,
    string? Description = null);
