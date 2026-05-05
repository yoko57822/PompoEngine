namespace Pompo.VisualScripting.Runtime;

using Pompo.Core.Localization;
using Pompo.Core.Runtime;

public sealed record RuntimeTraceEvent(
    string Kind,
    string GraphId,
    int InstructionPointer,
    string? Text = null,
    string? Speaker = null,
    IReadOnlyList<string>? Choices = null,
    string? SelectedChoice = null,
    IReadOnlyDictionary<string, object?>? Variables = null);

public sealed record RuntimeTraceResult(
    string GraphId,
    bool Completed,
    IReadOnlyList<RuntimeTraceEvent> Events,
    IReadOnlyDictionary<string, object?> Variables,
    string? BackgroundAssetId,
    IReadOnlyList<RuntimeCharacterState> Characters,
    RuntimeAudioState Audio,
    IReadOnlyList<string> ChoiceHistory)
{
    public RuntimeSaveData? SaveData { get; init; }
    public IReadOnlyList<string> UnlockedCgIds { get; init; } = [];
}

public sealed class RuntimeTraceRunner
{
    public RuntimeTraceResult Run(
        PompoGraphIR ir,
        IReadOnlyList<int>? choiceSelections = null,
        int maxSteps = 1_000,
        StringTableLocalizer? localizer = null,
        IReadOnlyDictionary<string, PompoGraphIR>? graphLibrary = null,
        IRuntimeCustomNodeHandler? customNodeHandler = null)
    {
        var runtime = new GraphRuntimeInterpreter(ir, localizer, graphLibrary, customNodeHandler);
        var events = new List<RuntimeTraceEvent>();
        var choiceIndex = 0;
        var steps = 0;

        while (!runtime.Snapshot.IsComplete)
        {
            if (++steps > maxSteps)
            {
                throw new InvalidOperationException($"Runtime exceeded max step count {maxSteps}.");
            }

            var snapshot = runtime.Step();
            if (snapshot.CurrentLine is not null)
            {
                events.Add(new RuntimeTraceEvent(
                    "line",
                    snapshot.GraphId,
                    snapshot.InstructionPointer,
                    snapshot.CurrentLine.Text,
                    snapshot.CurrentLine.Speaker,
                    Variables: snapshot.Variables));
            }

            if (snapshot.Choices.Count > 0)
            {
                var requestedIndex = choiceSelections is not null && choiceIndex < choiceSelections.Count
                    ? choiceSelections[choiceIndex]
                    : (int?)null;
                var selectedIndex = ResolveChoiceSelection(snapshot.Choices, requestedIndex);
                var selectedChoice = snapshot.Choices[selectedIndex];
                events.Add(new RuntimeTraceEvent(
                    "choice",
                    snapshot.GraphId,
                    snapshot.InstructionPointer,
                    Choices: snapshot.Choices.Select(choice => choice.Text).ToArray(),
                    SelectedChoice: selectedChoice.Text,
                    Variables: snapshot.Variables));
                runtime.Choose(selectedIndex);
                choiceIndex++;
            }
        }

        var final = runtime.Snapshot;
        events.Add(new RuntimeTraceEvent(
            "complete",
            final.GraphId,
            final.InstructionPointer,
            Variables: final.Variables));

        return new RuntimeTraceResult(
            ir.GraphId,
            final.IsComplete,
            events,
            final.Variables,
            final.BackgroundAssetId,
            final.Characters,
            final.Audio,
            final.ChoiceHistory)
        {
            SaveData = runtime.CreateSaveData(),
            UnlockedCgIds = final.UnlockedCgIds
        };
    }

    private static int ResolveChoiceSelection(IReadOnlyList<RuntimeChoice> choices, int? requestedIndex)
    {
        if (requestedIndex is not null)
        {
            if (requestedIndex < 0 || requestedIndex >= choices.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requestedIndex),
                    requestedIndex,
                    $"Choice index {requestedIndex} is outside the available range 0..{choices.Count - 1}.");
            }

            if (!choices[requestedIndex.Value].IsEnabled)
            {
                throw new InvalidOperationException($"Choice '{choices[requestedIndex.Value].Text}' is disabled.");
            }

            return requestedIndex.Value;
        }

        return FirstEnabledChoiceIndex(choices);
    }

    private static int FirstEnabledChoiceIndex(IReadOnlyList<RuntimeChoice> choices)
    {
        for (var index = 0; index < choices.Count; index++)
        {
            if (choices[index].IsEnabled)
            {
                return index;
            }
        }

        throw new InvalidOperationException("Runtime choice stop has no enabled choices.");
    }
}
