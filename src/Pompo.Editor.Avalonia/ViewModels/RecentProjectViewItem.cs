namespace Pompo.Editor.Avalonia.ViewModels;

public sealed record RecentProjectViewItem(
    string ProjectName,
    string ProjectRoot,
    string LastOpenedLocal,
    bool IsMissing,
    bool IsSelected);
