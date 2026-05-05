namespace Pompo.Editor.Avalonia.ViewModels;

public sealed record ProjectDashboardSummary(
    string ProjectName,
    string ProjectRoot,
    int SceneCount,
    int CharacterCount,
    int GraphCount,
    int AssetCount,
    int DiagnosticCount,
    int BrokenAssetCount,
    bool IsValid);
