using Pompo.VisualScripting.Runtime;
using Pompo.Core.Project;
using Pompo.Core.Runtime;

namespace Pompo.Runtime.Fna.Presentation;

public sealed record UiRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}

public sealed record RuntimeChoiceUi(string Text, UiRect Bounds, bool IsSelected, bool IsEnabled = true);

public sealed record RuntimeDialogueUi(
    string? Speaker,
    string Text,
    bool IsNarration,
    UiRect NameBox,
    UiRect TextBox,
    IReadOnlyList<RuntimeChoiceUi> Choices);

public sealed record RuntimeSaveSlotUi(
    string SlotId,
    string DisplayName,
    string Location,
    string SavedAt,
    UiRect Bounds,
    bool IsSelected,
    bool IsEmpty = false);

public sealed record RuntimeSaveMenuUi(
    string Title,
    UiRect Bounds,
    RuntimeSaveSlotUi QuickSlot,
    IReadOnlyList<RuntimeSaveSlotUi> Slots,
    string HelpText);

public static class RuntimeSaveMenuHitTest
{
    public static string? GetSlotIdAt(RuntimeSaveMenuUi? saveMenu, int x, int y)
    {
        if (saveMenu is null)
        {
            return null;
        }

        if (Contains(saveMenu.QuickSlot.Bounds, x, y))
        {
            return saveMenu.QuickSlot.SlotId;
        }

        foreach (var slot in saveMenu.Slots)
        {
            if (Contains(slot.Bounds, x, y))
            {
                return slot.SlotId;
            }
        }

        return null;
    }

    private static bool Contains(UiRect rect, int x, int y)
    {
        return x >= rect.X && x <= rect.Right && y >= rect.Y && y <= rect.Bottom;
    }
}

public sealed record RuntimeBacklogLineUi(string? Speaker, string Text);

public sealed record RuntimeBacklogUi(
    string Title,
    UiRect Bounds,
    IReadOnlyList<RuntimeBacklogLineUi> Lines);

public sealed record RuntimeUiFrame(
    int VirtualWidth,
    int VirtualHeight,
    UiRect Canvas,
    string? BackgroundAssetId,
    IReadOnlyList<RuntimeCharacterState> Characters,
    RuntimeAudioState Audio,
    RuntimeDialogueUi? Dialogue,
    RuntimeSaveMenuUi? SaveMenu = null,
    RuntimeSaveData? CurrentSaveData = null,
    RuntimeBacklogUi? Backlog = null);

public sealed class RuntimeUiLayout
{
    private readonly PompoRuntimeUiLayoutSettings _settings;

    public RuntimeUiLayout(PompoRuntimeUiLayoutSettings? settings = null)
    {
        _settings = settings ?? new PompoRuntimeUiLayoutSettings();
    }

    public RuntimeUiFrame CreateFrame(
        RuntimeTraceResult trace,
        IReadOnlyList<RuntimeSaveSlotMetadata>? saveSlots = null,
        string? selectedSaveSlotId = null,
        int virtualWidth = 1920,
        int virtualHeight = 1080)
    {
        var lastLine = trace.Events.LastOrDefault(item => item.Kind == "line");
        var lastChoice = trace.Events.LastOrDefault(item => item.Kind == "choice");
        var saveMenu = CreateSaveMenu(saveSlots, selectedSaveSlotId, virtualWidth, virtualHeight);
        var backlog = CreateBacklog(trace.Events, virtualWidth, virtualHeight);
        if (lastLine is null && lastChoice is null)
        {
            return new RuntimeUiFrame(
                virtualWidth,
                virtualHeight,
                new UiRect(0, 0, virtualWidth, virtualHeight),
                trace.BackgroundAssetId,
                trace.Characters.Where(character => character.Visible).ToArray(),
                trace.Audio,
                null,
                saveMenu,
                trace.SaveData,
                backlog);
        }

        var textBox = ToUiRect(_settings.DialogueTextBox);
        var nameBox = ToUiRect(_settings.DialogueNameBox);
        var choices = CreateChoices(lastChoice, virtualWidth, virtualHeight);
        var text = lastLine?.Text ?? lastChoice?.Text ?? string.Empty;

        return new RuntimeUiFrame(
            virtualWidth,
            virtualHeight,
            new UiRect(0, 0, virtualWidth, virtualHeight),
            trace.BackgroundAssetId,
            trace.Characters.Where(character => character.Visible).ToArray(),
            trace.Audio,
            new RuntimeDialogueUi(
                lastLine?.Speaker,
                text ?? string.Empty,
                lastLine?.Speaker is null,
                nameBox,
                textBox,
                choices),
            saveMenu,
            trace.SaveData,
            backlog);
    }

    public RuntimeUiFrame CreateFrame(
        RuntimeExecutionSnapshot snapshot,
        IReadOnlyList<RuntimeSaveSlotMetadata>? saveSlots = null,
        RuntimeSaveData? currentSaveData = null,
        string? selectedSaveSlotId = null,
        int selectedChoiceIndex = 0,
        IReadOnlyList<RuntimeBacklogLineUi>? backlogLines = null,
        int virtualWidth = 1920,
        int virtualHeight = 1080)
    {
        var saveMenu = CreateSaveMenu(saveSlots, selectedSaveSlotId, virtualWidth, virtualHeight);
        var backlog = CreateBacklog(backlogLines, virtualWidth, virtualHeight);
        RuntimeDialogueUi? dialogue = null;
        if (snapshot.CurrentLine is not null || snapshot.Choices.Count > 0)
        {
            var textBox = ToUiRect(_settings.DialogueTextBox);
            var nameBox = ToUiRect(_settings.DialogueNameBox);
            dialogue = new RuntimeDialogueUi(
                snapshot.CurrentLine?.Speaker,
                snapshot.CurrentLine?.Text ?? string.Empty,
                snapshot.CurrentLine?.IsNarration ?? true,
                nameBox,
                textBox,
                CreateChoices(snapshot.Choices, selectedChoiceIndex, virtualWidth, virtualHeight));
        }

        return new RuntimeUiFrame(
            virtualWidth,
            virtualHeight,
            new UiRect(0, 0, virtualWidth, virtualHeight),
            snapshot.BackgroundAssetId,
            snapshot.Characters.Where(character => character.Visible).ToArray(),
            snapshot.Audio,
            dialogue,
            saveMenu,
            currentSaveData,
            backlog);
    }

    public RuntimeUiFrame WithSaveMenu(
        RuntimeUiFrame frame,
        IReadOnlyList<RuntimeSaveSlotMetadata>? saveSlots,
        string? selectedSaveSlotId = null)
    {
        return frame with
        {
            SaveMenu = CreateSaveMenu(saveSlots, selectedSaveSlotId, frame.VirtualWidth, frame.VirtualHeight)
        };
    }

    private IReadOnlyList<RuntimeChoiceUi> CreateChoices(
        RuntimeTraceEvent? choiceEvent,
        int virtualWidth,
        int virtualHeight)
    {
        if (choiceEvent?.Choices is null || choiceEvent.Choices.Count == 0)
        {
            return [];
        }

        var width = _settings.ChoiceBoxWidth;
        var height = _settings.ChoiceBoxHeight;
        var spacing = _settings.ChoiceBoxSpacing;
        var totalHeight = (choiceEvent.Choices.Count * height) + ((choiceEvent.Choices.Count - 1) * spacing);
        var startY = (virtualHeight - totalHeight) / 2;
        var x = (virtualWidth - width) / 2;

        return choiceEvent.Choices.Select((choice, index) =>
            new RuntimeChoiceUi(
                choice,
                new UiRect(x, startY + (index * (height + spacing)), width, height),
                string.Equals(choice, choiceEvent.SelectedChoice, StringComparison.Ordinal)))
            .ToArray();
    }

    private RuntimeSaveMenuUi? CreateSaveMenu(
        IReadOnlyList<RuntimeSaveSlotMetadata>? saveSlots,
        string? selectedSaveSlotId,
        int virtualWidth,
        int virtualHeight)
    {
        if (saveSlots is null)
        {
            return null;
        }

        var bounds = ToUiRect(_settings.SaveMenuBounds);
        var slotHeight = _settings.SaveSlotHeight;
        var spacing = _settings.SaveSlotSpacing;
        var slotById = saveSlots.ToDictionary(slot => slot.SlotId, StringComparer.Ordinal);
        var selected = IsManualSlotId(selectedSaveSlotId) ? selectedSaveSlotId! : "manual_1";
        var quickSlot = CreateSaveSlotUi(
            "quick",
            "Quick Save",
            slotById.TryGetValue("quick", out var quick) ? quick : null,
            new UiRect(bounds.X + 28, bounds.Y + 82, bounds.Width - 56, slotHeight),
            false);
        var slots = Enumerable.Range(1, 6)
            .Select(index =>
            {
                var slotId = $"manual_{index}";
                return CreateSaveSlotUi(
                    slotId,
                    $"Manual {index}",
                    slotById.TryGetValue(slotId, out var slot) ? slot : null,
                    new UiRect(bounds.X + 28, bounds.Y + 206 + ((index - 1) * (slotHeight + spacing)), bounds.Width - 56, slotHeight),
                    string.Equals(slotId, selected, StringComparison.Ordinal));
            })
            .ToArray();

        return new RuntimeSaveMenuUi(
            "Save / Load",
            bounds,
            quickSlot,
            slots,
            "Mouse hover select   Click load   F5 quick save   F6 save slot   F9 quick load   Enter load slot");
    }

    private static RuntimeSaveSlotUi CreateSaveSlotUi(
        string slotId,
        string fallbackDisplayName,
        RuntimeSaveSlotMetadata? metadata,
        UiRect bounds,
        bool isSelected)
    {
        return new RuntimeSaveSlotUi(
            slotId,
            metadata?.DisplayName ?? fallbackDisplayName,
            metadata is null ? "Empty" : $"{metadata.GraphId}:{metadata.NodeId}",
            metadata is null ? "No save data" : metadata.SavedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            bounds,
            isSelected,
            metadata is null);
    }

    private static bool IsManualSlotId(string? slotId)
    {
        return slotId is "manual_1" or "manual_2" or "manual_3" or "manual_4" or "manual_5" or "manual_6";
    }

    private RuntimeBacklogUi? CreateBacklog(
        IReadOnlyList<RuntimeTraceEvent> events,
        int virtualWidth,
        int virtualHeight)
    {
        var lines = events
            .Where(traceEvent => traceEvent.Kind == "line" && !string.IsNullOrWhiteSpace(traceEvent.Text))
            .TakeLast(8)
            .Select(traceEvent => new RuntimeBacklogLineUi(traceEvent.Speaker, traceEvent.Text ?? string.Empty))
            .ToArray();

        if (lines.Length == 0)
        {
            return null;
        }

        return new RuntimeBacklogUi(
            "Backlog",
            ToUiRect(_settings.BacklogBounds),
            lines);
    }

    private RuntimeBacklogUi? CreateBacklog(
        IReadOnlyList<RuntimeBacklogLineUi>? lines,
        int virtualWidth,
        int virtualHeight)
    {
        if (lines is null || lines.Count == 0)
        {
            return null;
        }

        return new RuntimeBacklogUi(
            "Backlog",
            ToUiRect(_settings.BacklogBounds),
            lines.TakeLast(8).ToArray());
    }

    private IReadOnlyList<RuntimeChoiceUi> CreateChoices(
        IReadOnlyList<RuntimeChoice> choices,
        int selectedChoiceIndex,
        int virtualWidth,
        int virtualHeight)
    {
        if (choices.Count == 0)
        {
            return [];
        }

        var width = _settings.ChoiceBoxWidth;
        var height = _settings.ChoiceBoxHeight;
        var spacing = _settings.ChoiceBoxSpacing;
        var totalHeight = (choices.Count * height) + ((choices.Count - 1) * spacing);
        var startY = (virtualHeight - totalHeight) / 2;
        var x = (virtualWidth - width) / 2;

        return choices.Select((choice, index) =>
            new RuntimeChoiceUi(
                choice.Text,
                new UiRect(x, startY + (index * (height + spacing)), width, height),
                index == Math.Clamp(selectedChoiceIndex, 0, choices.Count - 1),
                choice.IsEnabled))
            .ToArray();
    }

    private static UiRect ToUiRect(PompoRuntimeUiRect rect)
    {
        return new UiRect(rect.X, rect.Y, rect.Width, rect.Height);
    }
}
