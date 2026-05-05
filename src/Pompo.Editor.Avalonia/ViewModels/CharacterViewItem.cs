namespace Pompo.Editor.Avalonia.ViewModels;

public sealed record CharacterViewItem(
    string CharacterId,
    string DisplayName,
    string DefaultExpression,
    int ExpressionCount,
    bool IsSelected);
