namespace Pompo.Editor.Avalonia.ViewModels;

public sealed record CharacterExpressionViewItem(
    string ExpressionId,
    string SpriteAssetId,
    string Description,
    bool IsSelected);
