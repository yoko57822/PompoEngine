namespace Pompo.Editor.Avalonia.ViewModels;

public sealed record SaveSlotViewItem(
    string SlotId,
    string DisplayName,
    string GraphId,
    string NodeId,
    DateTimeOffset SavedAt)
{
    public string Location => $"{GraphId}:{NodeId}";

    public string SavedAtLocal => SavedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}
