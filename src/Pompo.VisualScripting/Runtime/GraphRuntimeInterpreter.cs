using System.Text.Json;
using System.Text.Json.Nodes;
using Pompo.Core;
using Pompo.Core.Graphs;
using Pompo.Core.Localization;
using Pompo.Core.Runtime;

namespace Pompo.VisualScripting.Runtime;

public sealed record RuntimeDialogueLine(string? Speaker, string Text, bool IsNarration);

public sealed record RuntimeChoice(string Text, int TargetInstruction, bool IsEnabled = true);

public sealed record RuntimeExecutionSnapshot(
    string GraphId,
    int InstructionPointer,
    bool IsComplete,
    IReadOnlyDictionary<string, object?> Variables,
    string? BackgroundAssetId,
    IReadOnlyList<RuntimeCharacterState> Characters,
    RuntimeAudioState Audio,
    RuntimeDialogueLine? CurrentLine,
    IReadOnlyList<RuntimeChoice> Choices,
    IReadOnlyList<string> ChoiceHistory)
{
    public IReadOnlyList<string> UnlockedCgIds { get; init; } = [];
    public IReadOnlyList<string> CallStack { get; init; } = [];
}

public sealed class GraphRuntimeInterpreter(
    PompoGraphIR graph,
    StringTableLocalizer? localizer = null,
    IReadOnlyDictionary<string, PompoGraphIR>? graphLibrary = null,
    IRuntimeCustomNodeHandler? customNodeHandler = null)
{
    private readonly Dictionary<string, PompoGraphIR> _graphs = graphLibrary is null
        ? new Dictionary<string, PompoGraphIR>(StringComparer.Ordinal) { [graph.GraphId] = graph }
        : graphLibrary.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
    private readonly Dictionary<string, object?> _variables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RuntimeCharacterState> _characters = new(StringComparer.Ordinal);
    private readonly List<string> _choiceHistory = [];
    private readonly List<string> _playingSfxAssetIds = [];
    private readonly HashSet<string> _unlockedCgIds = new(StringComparer.Ordinal);
    private readonly Stack<RuntimeCallFrame> _callStack = new();
    private PompoGraphIR _graph = graph;
    private string? _backgroundAssetId;
    private string? _bgmAssetId;
    private string? _voiceAssetId;
    private int _instructionPointer;
    private RuntimeDialogueLine? _currentLine;
    private IReadOnlyList<RuntimeChoice> _choices = [];
    private bool _isComplete;

    public RuntimeExecutionSnapshot Snapshot => new(
            _graph.GraphId,
            _instructionPointer,
            _isComplete,
            new Dictionary<string, object?>(_variables, StringComparer.Ordinal),
            _backgroundAssetId,
            _characters.Values.OrderBy(character => character.Layer).ThenBy(character => character.CharacterId, StringComparer.Ordinal).ToArray(),
            new RuntimeAudioState(_bgmAssetId, _playingSfxAssetIds.ToArray()) { VoiceAssetId = _voiceAssetId },
            _currentLine,
            _choices,
            _choiceHistory.ToArray())
        {
            UnlockedCgIds = _unlockedCgIds.Order(StringComparer.Ordinal).ToArray(),
            CallStack = _callStack.Reverse().Select(FormatCallFrame).ToArray()
        };

    public RuntimeExecutionSnapshot Step()
    {
        if (_isComplete || _instructionPointer < 0 || _instructionPointer >= _graph.Instructions.Count)
        {
            _isComplete = true;
            return Snapshot;
        }

        _currentLine = null;
        _choices = [];

        var instruction = _graph.Instructions[_instructionPointer];
        switch (instruction.Operation)
        {
            case GraphNodeKind.Start:
            case GraphNodeKind.Fade:
            case GraphNodeKind.Wait:
            case GraphNodeKind.SavePoint:
                MoveNext(instruction, "out");
                break;

            case GraphNodeKind.MoveCharacter:
                MoveCharacter(instruction);
                MoveNext(instruction, "out");
                break;

            case GraphNodeKind.UnlockCg:
                UnlockCg(instruction);
                MoveNext(instruction, "out");
                break;

            case GraphNodeKind.PlayBgm:
                _bgmAssetId = ReadString(instruction, "assetId") ?? ReadString(instruction, "bgmAssetId");
                MoveNext(instruction, "out");
                break;

            case GraphNodeKind.StopBgm:
                _bgmAssetId = null;
                MoveNext(instruction, "out");
                break;

            case GraphNodeKind.PlaySfx:
                PlaySfx(instruction);
                MoveNext(instruction, "out");
                break;

            case GraphNodeKind.PlayVoice:
                _voiceAssetId = ReadString(instruction, "assetId") ?? ReadString(instruction, "voiceAssetId");
                MoveNext(instruction, "out");
                break;

            case GraphNodeKind.StopVoice:
                _voiceAssetId = null;
                MoveNext(instruction, "out");
                break;

            case GraphNodeKind.ChangeBackground:
                _backgroundAssetId = ReadString(instruction, "assetId") ?? ReadString(instruction, "backgroundAssetId");
                MoveNext(instruction, "out");
                break;

            case GraphNodeKind.ShowCharacter:
                ShowCharacter(instruction);
                MoveNext(instruction, "out");
                break;

            case GraphNodeKind.HideCharacter:
                HideCharacter(instruction);
                MoveNext(instruction, "out");
                break;

            case GraphNodeKind.ChangeExpression:
                ChangeExpression(instruction);
                MoveNext(instruction, "out");
                break;

            case GraphNodeKind.Dialogue:
                _currentLine = new RuntimeDialogueLine(
                    ResolveInstructionText(instruction, "speaker", "speakerKey", "speakerTableId"),
                    ResolveInstructionText(instruction, "text", "textKey", "tableId"),
                    false);
                MoveNext(instruction, "out");
                break;

            case GraphNodeKind.Narration:
                _currentLine = new RuntimeDialogueLine(
                    null,
                    ResolveInstructionText(instruction, "text", "textKey", "tableId"),
                    true);
                MoveNext(instruction, "out");
                break;

            case GraphNodeKind.SetVariable:
                SetVariable(instruction);
                MoveNext(instruction, "out");
                break;

            case GraphNodeKind.Branch:
                MoveNext(instruction, EvaluateCondition(instruction) ? "true" : "false");
                break;

            case GraphNodeKind.Choice:
                _choices = BuildChoices(instruction);
                if (_choices.Count == 0)
                {
                    MoveNext(instruction, "choice");
                }
                break;

            case GraphNodeKind.CallGraph:
                CallGraph(instruction);
                break;

            case GraphNodeKind.Return:
                ReturnFromGraph();
                break;

            case GraphNodeKind.Jump:
                JumpToTarget(instruction);
                break;

            case GraphNodeKind.EndScene:
            case GraphNodeKind.End:
                _isComplete = true;
                break;

            case GraphNodeKind.Custom:
                ExecuteCustomNode(instruction);
                break;

            default:
                throw new NotSupportedException($"Runtime operation '{instruction.Operation}' is not supported yet.");
        }

        return Snapshot;
    }

    public RuntimeExecutionSnapshot Choose(int index)
    {
        if (index < 0 || index >= _choices.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var choice = _choices[index];
        if (!choice.IsEnabled)
        {
            throw new InvalidOperationException($"Choice '{choice.Text}' is disabled.");
        }

        _choiceHistory.Add(choice.Text);
        _instructionPointer = choice.TargetInstruction;
        _choices = [];
        return Snapshot;
    }

    public RuntimeSaveData CreateSaveData()
    {
        var nodeId = _instructionPointer >= 0 && _instructionPointer < _graph.Instructions.Count
            ? _graph.Instructions[_instructionPointer].SourceNodeId
            : string.Empty;

        return new RuntimeSaveData(
                ProjectConstants.CurrentSchemaVersion,
                _graph.GraphId,
                nodeId,
                _callStack.Reverse().Select(FormatCallFrame).ToArray(),
                new Dictionary<string, object?>(_variables, StringComparer.Ordinal),
                _backgroundAssetId,
                Snapshot.Characters,
                Snapshot.Audio,
                _choiceHistory.ToArray())
            {
                UnlockedCgIds = _unlockedCgIds.Order(StringComparer.Ordinal).ToArray()
            };
    }

    public static GraphRuntimeInterpreter FromSaveData(
        PompoGraphIR graph,
        RuntimeSaveData saveData,
        StringTableLocalizer? localizer = null,
        IRuntimeCustomNodeHandler? customNodeHandler = null)
    {
        return FromSaveData(
            new Dictionary<string, PompoGraphIR>(StringComparer.Ordinal) { [graph.GraphId] = graph },
            saveData,
            localizer,
            customNodeHandler);
    }

    public static GraphRuntimeInterpreter FromSaveData(
        IReadOnlyDictionary<string, PompoGraphIR> graphLibrary,
        RuntimeSaveData saveData,
        StringTableLocalizer? localizer = null,
        IRuntimeCustomNodeHandler? customNodeHandler = null)
    {
        if (!graphLibrary.TryGetValue(saveData.GraphId, out var currentGraph))
        {
            throw new InvalidDataException($"Save data graph '{saveData.GraphId}' does not exist in the IR library.");
        }

        var instructionPointer = -1;
        for (var index = 0; index < currentGraph.Instructions.Count; index++)
        {
            if (string.Equals(currentGraph.Instructions[index].SourceNodeId, saveData.NodeId, StringComparison.Ordinal))
            {
                instructionPointer = index;
                break;
            }
        }

        if (instructionPointer < 0)
        {
            throw new InvalidDataException($"Save data node '{saveData.NodeId}' does not exist in graph '{currentGraph.GraphId}'.");
        }

        var runtime = new GraphRuntimeInterpreter(currentGraph, localizer, graphLibrary, customNodeHandler)
        {
            _instructionPointer = instructionPointer
        };

        foreach (var variable in saveData.Variables)
        {
            runtime._variables[variable.Key] = variable.Value;
        }

        runtime._backgroundAssetId = saveData.BackgroundAssetId;
        runtime._bgmAssetId = saveData.Audio.BgmAssetId;
        runtime._voiceAssetId = saveData.Audio.VoiceAssetId;
        runtime._playingSfxAssetIds.AddRange(saveData.Audio.PlayingSfxAssetIds);
        foreach (var character in saveData.Characters)
        {
            runtime._characters[character.CharacterId] = character;
        }

        runtime._choiceHistory.AddRange(saveData.ChoiceHistory);
        foreach (var frame in saveData.CallStack.Select(ParseCallFrame))
        {
            if (!runtime._graphs.ContainsKey(frame.GraphId))
            {
                throw new InvalidDataException($"Save data call stack graph '{frame.GraphId}' does not exist in the IR library.");
            }

            runtime._callStack.Push(frame);
        }

        foreach (var cgId in saveData.UnlockedCgIds)
        {
            runtime._unlockedCgIds.Add(cgId);
        }

        return runtime;
    }

    private sealed record RuntimeCallFrame(string GraphId, int ReturnInstructionPointer);

    private void MoveNext(PompoIRInstruction instruction, string portName)
    {
        if (instruction.Jumps.TryGetValue(portName, out var target))
        {
            _instructionPointer = target;
            return;
        }

        if (instruction.Jumps.Count == 1)
        {
            _instructionPointer = instruction.Jumps.Values.Single();
            return;
        }

        _isComplete = true;
    }

    private int ResolveNextInstructionPointer(PompoIRInstruction instruction, string portName)
    {
        if (instruction.Jumps.TryGetValue(portName, out var target))
        {
            return target;
        }

        return instruction.Jumps.Count == 1 ? instruction.Jumps.Values.Single() : -1;
    }

    private void CallGraph(PompoIRInstruction instruction)
    {
        var targetGraphId = ReadString(instruction, "graphId") ?? ReadString(instruction, "targetGraphId");
        if (string.IsNullOrWhiteSpace(targetGraphId) ||
            !_graphs.TryGetValue(targetGraphId, out var targetGraph))
        {
            MoveNext(instruction, "out");
            return;
        }

        _callStack.Push(new RuntimeCallFrame(_graph.GraphId, ResolveNextInstructionPointer(instruction, "out")));
        _graph = targetGraph;
        _instructionPointer = 0;
    }

    private void ReturnFromGraph()
    {
        if (_callStack.Count == 0)
        {
            _isComplete = true;
            return;
        }

        var frame = _callStack.Pop();
        if (!_graphs.TryGetValue(frame.GraphId, out var graphToResume) ||
            frame.ReturnInstructionPointer < 0 ||
            frame.ReturnInstructionPointer >= graphToResume.Instructions.Count)
        {
            _isComplete = true;
            return;
        }

        _graph = graphToResume;
        _instructionPointer = frame.ReturnInstructionPointer;
    }

    private void JumpToTarget(PompoIRInstruction instruction)
    {
        var targetNodeId = ReadString(instruction, "targetNodeId") ?? ReadString(instruction, "nodeId");
        if (string.IsNullOrWhiteSpace(targetNodeId))
        {
            MoveNext(instruction, "out");
            return;
        }

        for (var index = 0; index < _graph.Instructions.Count; index++)
        {
            if (string.Equals(_graph.Instructions[index].SourceNodeId, targetNodeId, StringComparison.Ordinal))
            {
                _instructionPointer = index;
                return;
            }
        }

        _isComplete = true;
    }

    private void SetVariable(PompoIRInstruction instruction)
    {
        var name = ReadString(instruction, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        _variables[name] = ReadValue(instruction.Arguments.TryGetValue("value", out var value) ? value : null);
    }

    private void PlaySfx(PompoIRInstruction instruction)
    {
        var assetId = ReadString(instruction, "assetId") ?? ReadString(instruction, "sfxAssetId");
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return;
        }

        _playingSfxAssetIds.Add(assetId);
    }

    private void ShowCharacter(PompoIRInstruction instruction)
    {
        var characterId = ReadString(instruction, "characterId") ?? ReadString(instruction, "id");
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return;
        }

        _characters[characterId] = new RuntimeCharacterState(
            characterId,
            ReadString(instruction, "expressionId") ?? ReadString(instruction, "expression") ?? "default",
            ReadLayer(instruction, RuntimeLayer.Character),
            ReadFloat(instruction, "x") ?? 0.5f,
            ReadFloat(instruction, "y") ?? 1f,
            true);
    }

    private void HideCharacter(PompoIRInstruction instruction)
    {
        var characterId = ReadString(instruction, "characterId") ?? ReadString(instruction, "id");
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return;
        }

        if (_characters.TryGetValue(characterId, out var state))
        {
            _characters[characterId] = state with { Visible = false };
        }
    }

    private void MoveCharacter(PompoIRInstruction instruction)
    {
        var characterId = ReadString(instruction, "characterId") ?? ReadString(instruction, "id");
        if (string.IsNullOrWhiteSpace(characterId) ||
            !_characters.TryGetValue(characterId, out var state))
        {
            return;
        }

        _characters[characterId] = state with
        {
            Layer = ReadLayer(instruction, state.Layer),
            X = ReadFloat(instruction, "x") ?? state.X,
            Y = ReadFloat(instruction, "y") ?? state.Y
        };
    }

    private void UnlockCg(PompoIRInstruction instruction)
    {
        var cgId = ReadString(instruction, "cgId") ??
            ReadString(instruction, "assetId") ??
            ReadString(instruction, "id");
        if (!string.IsNullOrWhiteSpace(cgId))
        {
            _unlockedCgIds.Add(cgId);
        }
    }

    private void ExecuteCustomNode(PompoIRInstruction instruction)
    {
        if (customNodeHandler is null)
        {
            throw new NotSupportedException(
                $"Custom runtime node '{instruction.SourceNodeId}' requires a custom node handler.");
        }

        var result = customNodeHandler.ExecuteAsync(
                new RuntimeCustomNodeContext(
                    _graph.GraphId,
                    instruction.SourceNodeId,
                    instruction.Arguments,
                    _variables))
            .AsTask()
            .GetAwaiter()
            .GetResult();
        MoveNext(instruction, result.OutputPort ?? "out");
    }

    private void ChangeExpression(PompoIRInstruction instruction)
    {
        var characterId = ReadString(instruction, "characterId") ?? ReadString(instruction, "id");
        if (string.IsNullOrWhiteSpace(characterId) ||
            !_characters.TryGetValue(characterId, out var state))
        {
            return;
        }

        _characters[characterId] = state with
        {
            ExpressionId = ReadString(instruction, "expressionId") ?? ReadString(instruction, "expression") ?? state.ExpressionId
        };
    }

    private bool EvaluateCondition(PompoIRInstruction instruction)
    {
        var variableName = ReadString(instruction, "variable");
        if (!string.IsNullOrWhiteSpace(variableName) &&
            _variables.TryGetValue(variableName, out var value) &&
            value is bool boolValue)
        {
            return boolValue;
        }

        if (instruction.Arguments.TryGetValue("condition", out var condition))
        {
            return ReadValue(condition) is true;
        }

        return false;
    }

    private IReadOnlyList<RuntimeChoice> BuildChoices(PompoIRInstruction instruction)
    {
        if (!instruction.Arguments.TryGetValue("choices", out var choicesNode) ||
            choicesNode is not JsonArray choicesArray)
        {
            return instruction.Jumps
                .Select(jump => new RuntimeChoice(jump.Key, jump.Value))
                .ToArray();
        }

        var choices = new List<RuntimeChoice>();
        for (var index = 0; index < choicesArray.Count; index++)
        {
            if (choicesArray[index] is not JsonObject choiceNode)
            {
                throw new InvalidDataException(
                    $"Choice instruction '{instruction.SourceNodeId}' has a malformed choice entry at index {index}.");
            }

            var port = ReadChoicePort(instruction, choiceNode, index);
            if (!instruction.Jumps.TryGetValue(port, out var target))
            {
                throw new InvalidDataException(
                    $"Choice instruction '{instruction.SourceNodeId}' references missing output port '{port}'.");
            }

            choices.Add(new RuntimeChoice(
                ResolveChoiceText(instruction, choiceNode, port),
                target,
                IsChoiceEnabled(choiceNode)));
        }

        return choices;
    }

    private static string ReadChoicePort(
        PompoIRInstruction instruction,
        JsonObject choiceNode,
        int index)
    {
        if (!choiceNode.TryGetPropertyValue("port", out var portNode) ||
            portNode is null)
        {
            return "choice";
        }

        if (portNode.GetValueKind() != JsonValueKind.String)
        {
            throw new InvalidDataException(
                $"Choice instruction '{instruction.SourceNodeId}' has a non-string port at index {index}.");
        }

        var port = portNode.GetValue<string>();
        if (string.IsNullOrWhiteSpace(port))
        {
            throw new InvalidDataException(
                $"Choice instruction '{instruction.SourceNodeId}' has an empty port at index {index}.");
        }

        return port;
    }

    private bool IsChoiceEnabled(JsonObject choiceNode)
    {
        if (choiceNode.TryGetPropertyValue("enabled", out var enabledNode) &&
            ReadValue(enabledNode) is bool enabled)
        {
            return enabled;
        }

        var variableName = ReadOptionalString(choiceNode, "enabledVariable");
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return true;
        }

        return _variables.TryGetValue(variableName, out var value) && value is true;
    }

    private static string FormatCallFrame(RuntimeCallFrame frame)
    {
        return $"{frame.GraphId}:{frame.ReturnInstructionPointer}";
    }

    private static RuntimeCallFrame ParseCallFrame(string serialized)
    {
        var separator = serialized.LastIndexOf(':');
        if (separator <= 0 ||
            separator == serialized.Length - 1 ||
            !int.TryParse(serialized[(separator + 1)..], out var returnInstructionPointer))
        {
            throw new InvalidDataException($"Save data call stack frame '{serialized}' is invalid.");
        }

        return new RuntimeCallFrame(serialized[..separator], returnInstructionPointer);
    }

    private string ResolveInstructionText(
        PompoIRInstruction instruction,
        string literalProperty,
        string keyProperty,
        string tableProperty)
    {
        var literal = ReadString(instruction, literalProperty);
        var key = ReadString(instruction, keyProperty);
        if (localizer is null || string.IsNullOrWhiteSpace(key))
        {
            return literal ?? string.Empty;
        }

        return localizer.Resolve(ReadString(instruction, tableProperty), key, literal);
    }

    private string ResolveChoiceText(
        PompoIRInstruction instruction,
        JsonObject choiceNode,
        string fallback)
    {
        var literal = ReadOptionalString(choiceNode, "text");
        var key = ReadOptionalString(choiceNode, "textKey");
        if (localizer is null || string.IsNullOrWhiteSpace(key))
        {
            return literal ?? fallback;
        }

        var tableId = ReadOptionalString(choiceNode, "tableId") ?? ReadString(instruction, "tableId");
        return localizer.Resolve(tableId, key, literal ?? fallback);
    }

    private static string? ReadOptionalString(JsonObject node, string propertyName)
    {
        if (!node.TryGetPropertyValue(propertyName, out var value) ||
            value is null ||
            value.GetValueKind() != JsonValueKind.String)
        {
            return null;
        }

        return value.GetValue<string>();
    }

    private static string? ReadString(PompoIRInstruction instruction, string name)
    {
        return instruction.Arguments.TryGetValue(name, out var node) ? node?.GetValue<string>() : null;
    }

    private static float? ReadFloat(PompoIRInstruction instruction, string name)
    {
        if (!instruction.Arguments.TryGetValue(name, out var node) || node is null)
        {
            return null;
        }

        if (node is JsonValue value && value.TryGetValue<float>(out var floatValue))
        {
            return floatValue;
        }

        return node is JsonValue doubleValue && doubleValue.TryGetValue<double>(out var valueAsDouble)
            ? (float)valueAsDouble
            : null;
    }

    private static RuntimeLayer ReadLayer(PompoIRInstruction instruction, RuntimeLayer fallback)
    {
        var layer = ReadString(instruction, "layer");
        return Enum.TryParse<RuntimeLayer>(layer, ignoreCase: true, out var parsed) ? parsed : fallback;
    }

    private static object? ReadValue(JsonNode? node)
    {
        return node switch
        {
            null => null,
            JsonValue value when value.TryGetValue<bool>(out var boolValue) => boolValue,
            JsonValue value when value.TryGetValue<int>(out var intValue) => intValue,
            JsonValue value when value.TryGetValue<float>(out var floatValue) => floatValue,
            JsonValue value when value.TryGetValue<string>(out var stringValue) => stringValue,
            _ => node.ToJsonString()
        };
    }
}
