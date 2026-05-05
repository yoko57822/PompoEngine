namespace Pompo.Editor.Avalonia.ViewModels;

public sealed record GraphPreviewEventItem(
    string Kind,
    string Text,
    string Detail);

public sealed record GraphPreviewState(
    bool Success,
    string Summary,
    IReadOnlyList<GraphPreviewEventItem> Events,
    IReadOnlyDictionary<string, object?> Variables,
    string AudioSummary,
    IReadOnlyList<EditorDiagnostic> Diagnostics)
{
    public string VariablesSummary => Variables.Count == 0
        ? "No variables"
        : string.Join(", ", Variables.Select(variable => $"{variable.Key}={variable.Value}"));
}
