namespace Pompo.Editor.Avalonia.ViewModels;

public sealed record EditorReadinessItem(
    string Title,
    string Status,
    string Detail,
    bool IsPassing,
    bool IsBlocking);
