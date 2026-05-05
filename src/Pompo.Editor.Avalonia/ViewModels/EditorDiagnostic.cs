namespace Pompo.Editor.Avalonia.ViewModels;

public sealed record EditorDiagnostic(
    string Code,
    string Message,
    string? DocumentPath,
    string? ElementId);
