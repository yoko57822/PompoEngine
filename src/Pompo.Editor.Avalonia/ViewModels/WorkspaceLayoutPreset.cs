namespace Pompo.Editor.Avalonia.ViewModels;

public sealed record WorkspaceLayoutPreset(
    string PresetId,
    string DisplayName,
    string Description,
    string ColumnDefinitions,
    string RowDefinitions);
