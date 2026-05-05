namespace Pompo.Editor.Avalonia.ViewModels;

public sealed record ProjectWorkspaceState(
    Pompo.Core.Project.PompoProjectDocument Project,
    ProjectDashboardSummary Summary,
    IReadOnlyList<ResourceItem> Resources,
    IReadOnlyList<EditorDiagnostic> Diagnostics,
    IReadOnlyList<Pompo.Core.Graphs.GraphDocument> Graphs);
