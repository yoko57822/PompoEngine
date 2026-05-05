namespace Pompo.Editor.Avalonia.ViewModels;

public sealed record BuildHistoryViewItem(
    string BuiltAtLocal,
    string ProfileName,
    string Platform,
    string OutputDirectory,
    string Status,
    string AppVersion);
