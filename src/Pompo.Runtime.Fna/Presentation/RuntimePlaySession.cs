using Pompo.Core.Project;
using Pompo.Core.Localization;
using Pompo.Core.Runtime;
using Pompo.VisualScripting;
using Pompo.VisualScripting.Runtime;

namespace Pompo.Runtime.Fna.Presentation;

public sealed class RuntimePlaySession
{
    private readonly IReadOnlyDictionary<string, PompoGraphIR> _graphLibrary;
    private readonly StringTableLocalizer? _localizer;
    private readonly IRuntimeCustomNodeHandler? _customNodeHandler;
    private readonly RuntimeUiLayout _layout;
    private readonly List<RuntimeBacklogLineUi> _backlog = [];
    private GraphRuntimeInterpreter _runtime;
    private IReadOnlyList<RuntimeSaveSlotMetadata>? _saveSlots;
    private string? _selectedSaveSlotId;
    private int _selectedChoiceIndex;

    public RuntimePlaySession(
        PompoGraphIR rootGraph,
        IReadOnlyDictionary<string, PompoGraphIR>? graphLibrary = null,
        StringTableLocalizer? localizer = null,
        IReadOnlyList<RuntimeSaveSlotMetadata>? saveSlots = null,
        string? selectedSaveSlotId = null,
        IRuntimeCustomNodeHandler? customNodeHandler = null,
        PompoRuntimeUiLayoutSettings? runtimeUiLayout = null)
    {
        _graphLibrary = graphLibrary ?? new Dictionary<string, PompoGraphIR>(StringComparer.Ordinal)
        {
            [rootGraph.GraphId] = rootGraph
        };
        _localizer = localizer;
        _customNodeHandler = customNodeHandler;
        _layout = new RuntimeUiLayout(runtimeUiLayout);
        _runtime = new GraphRuntimeInterpreter(rootGraph, localizer, _graphLibrary, customNodeHandler);
        _saveSlots = saveSlots;
        _selectedSaveSlotId = selectedSaveSlotId;
        Frame = CreateFrame();
        AdvanceToNextStop();
    }

    public RuntimeUiFrame Frame { get; private set; }

    public RuntimeExecutionSnapshot Snapshot => _runtime.Snapshot;

    public bool HasChoices => Snapshot.Choices.Count > 0;

    public int SelectedChoiceIndex => _selectedChoiceIndex;

    public void AdvanceOrChoose()
    {
        if (Snapshot.IsComplete)
        {
            return;
        }

        if (Snapshot.Choices.Count > 0)
        {
            ChooseSelected();
            return;
        }

        AdvanceToNextStop();
    }

    public void SelectChoiceRelative(int offset)
    {
        var choices = Snapshot.Choices;
        if (choices.Count == 0)
        {
            return;
        }

        var enabledIndexes = choices
            .Select((choice, index) => (choice, index))
            .Where(item => item.choice.IsEnabled)
            .Select(item => item.index)
            .ToArray();
        if (enabledIndexes.Length == 0)
        {
            return;
        }

        var currentEnabledIndex = Array.IndexOf(enabledIndexes, _selectedChoiceIndex);
        if (currentEnabledIndex < 0)
        {
            currentEnabledIndex = 0;
        }

        _selectedChoiceIndex = enabledIndexes[(currentEnabledIndex + offset + enabledIndexes.Length) % enabledIndexes.Length];
        Frame = CreateFrame();
    }

    public void ChooseSelected()
    {
        if (Snapshot.Choices.Count == 0)
        {
            return;
        }

        var index = Math.Clamp(_selectedChoiceIndex, 0, Snapshot.Choices.Count - 1);
        if (!Snapshot.Choices[index].IsEnabled)
        {
            return;
        }

        _runtime.Choose(index);
        _selectedChoiceIndex = 0;
        AdvanceToNextStop();
    }

    public void ChooseAtVirtualPoint(int x, int y)
    {
        if (Frame.Dialogue is null)
        {
            AdvanceOrChoose();
            return;
        }

        for (var index = 0; index < Frame.Dialogue.Choices.Count; index++)
        {
            if (Contains(Frame.Dialogue.Choices[index].Bounds, x, y))
            {
                if (!Frame.Dialogue.Choices[index].IsEnabled)
                {
                    return;
                }

                _selectedChoiceIndex = index;
                ChooseSelected();
                return;
            }
        }

        AdvanceOrChoose();
    }

    public bool SelectChoiceAtVirtualPoint(int x, int y)
    {
        if (Frame.Dialogue is null || Frame.Dialogue.Choices.Count == 0)
        {
            return false;
        }

        for (var index = 0; index < Frame.Dialogue.Choices.Count; index++)
        {
            if (!Contains(Frame.Dialogue.Choices[index].Bounds, x, y))
            {
                continue;
            }

            if (!Frame.Dialogue.Choices[index].IsEnabled)
            {
                return false;
            }

            if (_selectedChoiceIndex == index)
            {
                return false;
            }

            _selectedChoiceIndex = index;
            Frame = CreateFrame();
            return true;
        }

        return false;
    }

    public void UpdateSaveSlots(
        IReadOnlyList<RuntimeSaveSlotMetadata>? saveSlots,
        string? selectedSaveSlotId)
    {
        _saveSlots = saveSlots;
        _selectedSaveSlotId = selectedSaveSlotId;
        Frame = CreateFrame();
    }

    public void RestoreFromSaveData(RuntimeSaveData saveData)
    {
        _runtime = GraphRuntimeInterpreter.FromSaveData(_graphLibrary, saveData, _localizer, _customNodeHandler);
        _selectedChoiceIndex = 0;
        AdvanceToNextStop();
    }

    private void AdvanceToNextStop(int maxSteps = 1_000)
    {
        var steps = 0;
        RuntimeExecutionSnapshot snapshot;
        do
        {
            if (++steps > maxSteps)
            {
                throw new InvalidOperationException($"Runtime exceeded max step count {maxSteps}.");
            }

            snapshot = _runtime.Step();
        }
        while (!snapshot.IsComplete && snapshot.CurrentLine is null && snapshot.Choices.Count == 0);

        if (snapshot.CurrentLine is not null)
        {
            _backlog.Add(new RuntimeBacklogLineUi(snapshot.CurrentLine.Speaker, snapshot.CurrentLine.Text));
        }

        EnsureSelectedChoiceIsEnabled(snapshot);
        Frame = CreateFrame();
    }

    private RuntimeUiFrame CreateFrame()
    {
        return _layout.CreateFrame(
            Snapshot,
            _saveSlots,
            _runtime.CreateSaveData(),
            _selectedSaveSlotId,
            _selectedChoiceIndex,
            _backlog);
    }

    private static bool Contains(UiRect rect, int x, int y)
    {
        return x >= rect.X && x <= rect.Right && y >= rect.Y && y <= rect.Bottom;
    }

    private void EnsureSelectedChoiceIsEnabled(RuntimeExecutionSnapshot snapshot)
    {
        if (snapshot.Choices.Count == 0)
        {
            _selectedChoiceIndex = 0;
            return;
        }

        if (_selectedChoiceIndex >= 0 &&
            _selectedChoiceIndex < snapshot.Choices.Count &&
            snapshot.Choices[_selectedChoiceIndex].IsEnabled)
        {
            return;
        }

        var firstEnabledIndex = snapshot.Choices
            .Select((choice, index) => (choice, index))
            .FirstOrDefault(item => item.choice.IsEnabled)
            .index;
        _selectedChoiceIndex = Math.Clamp(firstEnabledIndex, 0, snapshot.Choices.Count - 1);
    }
}
