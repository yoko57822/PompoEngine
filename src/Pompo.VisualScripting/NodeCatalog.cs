using System.Text.Json.Nodes;
using Pompo.Core.Graphs;

namespace Pompo.VisualScripting;

public sealed record PortContract(string Name, GraphPortKind Kind, GraphValueType ValueType, bool IsInput);

public sealed record NodePropertyContract(
    string Name,
    GraphValueType ValueType,
    bool IsRequired,
    string Description,
    JsonNode? DefaultValue = null,
    IReadOnlyList<string>? AlternativeNames = null);

public sealed record NodeDefinition(
    GraphNodeKind Kind,
    IReadOnlyList<PortContract> RequiredPorts,
    IReadOnlyList<NodePropertyContract> Properties)
{
    public NodeDefinition(GraphNodeKind kind, IReadOnlyList<PortContract> requiredPorts)
        : this(kind, requiredPorts, [])
    {
    }
}

public static class NodeCatalog
{
    public static IReadOnlyList<NodeDefinition> Definitions { get; } =
    [
        Flow(GraphNodeKind.Start, hasInput: false),
        Flow(GraphNodeKind.Dialogue, properties:
        [
            Property("speaker", GraphValueType.String, false, "Speaker display name."),
            Property("text", GraphValueType.String, true, "Dialogue line text.", "", ["textKey"])
        ]),
        Flow(GraphNodeKind.Narration, properties:
        [
            Property("text", GraphValueType.String, true, "Narration line text.", "", ["textKey"])
        ]),
        new(
            GraphNodeKind.Choice,
            [InExec()],
            [
                Property(
                    "choices",
                    GraphValueType.None,
                    false,
                    "Choice array with text, output port, optional enabled bool, and optional enabledVariable bool variable.",
                    new JsonArray
                    {
                        new JsonObject { ["text"] = "Left", ["port"] = "left", ["enabled"] = true },
                        new JsonObject { ["text"] = "Right", ["port"] = "right", ["enabled"] = true }
                    })
            ]),
        Flow(GraphNodeKind.SetVariable, properties:
        [
            Property("name", GraphValueType.String, true, "Variable name.", "flag"),
            Property("valueType", GraphValueType.Enum, false, "bool, int, float, string, enum, or asset reference.", "bool"),
            Property("value", GraphValueType.None, true, "Value to assign.", false)
        ]),
        new(
            GraphNodeKind.Branch,
            [InExec(), OutExec("true"), OutExec("false"), InData("condition", GraphValueType.Bool)],
            [
                Property("variable", GraphValueType.String, false, "Bool variable to evaluate."),
                Property("condition", GraphValueType.Bool, false, "Literal condition fallback.", false)
            ]),
        Flow(GraphNodeKind.Jump, properties:
        [
            Property("targetNodeId", GraphValueType.String, true, "Node id in the current graph.", alternativeNames: ["nodeId"])
        ]),
        Flow(GraphNodeKind.CallGraph, properties:
        [
            Property("graphId", GraphValueType.String, true, "Graph id to call.", alternativeNames: ["targetGraphId"])
        ]),
        Flow(GraphNodeKind.Return, hasOutput: false),
        Flow(GraphNodeKind.ShowCharacter, properties:
        [
            Property("characterId", GraphValueType.String, true, "Character id.", alternativeNames: ["id"]),
            Property("expressionId", GraphValueType.String, false, "Expression id.", "default", ["expression"]),
            Property("x", GraphValueType.Float, false, "Normalized canvas X position.", 0.5),
            Property("y", GraphValueType.Float, false, "Normalized canvas Y position.", 1.0),
            Property("layer", GraphValueType.Enum, false, "Runtime layer name.", "Character")
        ]),
        Flow(GraphNodeKind.HideCharacter, properties:
        [
            Property("characterId", GraphValueType.String, true, "Character id.", alternativeNames: ["id"])
        ]),
        Flow(GraphNodeKind.MoveCharacter, properties:
        [
            Property("characterId", GraphValueType.String, true, "Character id.", alternativeNames: ["id"]),
            Property("x", GraphValueType.Float, false, "Normalized canvas X position.", 0.5),
            Property("y", GraphValueType.Float, false, "Normalized canvas Y position.", 1.0),
            Property("layer", GraphValueType.Enum, false, "Runtime layer name.", "Character")
        ]),
        Flow(GraphNodeKind.ChangeExpression, properties:
        [
            Property("characterId", GraphValueType.String, true, "Character id.", alternativeNames: ["id"]),
            Property("expressionId", GraphValueType.String, true, "Expression id.", alternativeNames: ["expression"])
        ]),
        Flow(GraphNodeKind.PlayBgm, properties:
        [
            Property("assetId", GraphValueType.AssetReference, true, "Audio asset id.", alternativeNames: ["bgmAssetId"])
        ]),
        Flow(GraphNodeKind.StopBgm),
        Flow(GraphNodeKind.PlaySfx, properties:
        [
            Property("assetId", GraphValueType.AssetReference, true, "Audio asset id.", alternativeNames: ["sfxAssetId"])
        ]),
        Flow(GraphNodeKind.PlayVoice, properties:
        [
            Property("assetId", GraphValueType.AssetReference, true, "Audio asset id.", alternativeNames: ["voiceAssetId"])
        ]),
        Flow(GraphNodeKind.StopVoice),
        Flow(GraphNodeKind.ChangeBackground, properties:
        [
            Property("assetId", GraphValueType.AssetReference, true, "Image asset id.", alternativeNames: ["backgroundAssetId"])
        ]),
        Flow(GraphNodeKind.Fade, properties:
        [
            Property("seconds", GraphValueType.Float, false, "Fade duration in seconds.", 0.5)
        ]),
        Flow(GraphNodeKind.Wait, properties:
        [
            Property("seconds", GraphValueType.Float, false, "Wait duration in seconds.", 1.0)
        ]),
        Flow(GraphNodeKind.SavePoint, properties:
        [
            Property("slotHint", GraphValueType.String, false, "Suggested auto-save slot id.")
        ]),
        Flow(GraphNodeKind.UnlockCg, properties:
        [
            Property("cgId", GraphValueType.String, true, "CG gallery id to unlock.", alternativeNames: ["assetId", "id"])
        ]),
        Flow(GraphNodeKind.EndScene, hasOutput: false),
        Flow(GraphNodeKind.End, hasOutput: false)
    ];

    public static NodeDefinition? Find(GraphNodeKind kind)
    {
        return Definitions.FirstOrDefault(definition => definition.Kind == kind);
    }

    public static JsonObject CreateDefaultProperties(GraphNodeKind kind)
    {
        var properties = new JsonObject();
        var definition = Find(kind);
        if (definition is null)
        {
            return properties;
        }

        foreach (var property in definition.Properties)
        {
            if (property.DefaultValue is not null)
            {
                properties[property.Name] = property.DefaultValue.DeepClone();
            }
            else if (property.IsRequired)
            {
                properties[property.Name] = CreateEmptyValue(property.ValueType);
            }
        }

        return properties;
    }

    public static GraphPort InExecPort(string id = "in")
    {
        return new GraphPort(id, "in", GraphPortKind.Execution, GraphValueType.None, true);
    }

    public static GraphPort OutExecPort(string id = "out", string name = "out")
    {
        return new GraphPort(id, name, GraphPortKind.Execution, GraphValueType.None, false);
    }

    private static NodeDefinition Flow(
        GraphNodeKind kind,
        bool hasInput = true,
        bool hasOutput = true,
        IReadOnlyList<NodePropertyContract>? properties = null)
    {
        var ports = new List<PortContract>();
        if (hasInput)
        {
            ports.Add(InExec());
        }

        if (hasOutput)
        {
            ports.Add(OutExec("out"));
        }

        return new NodeDefinition(kind, ports, properties ?? []);
    }

    private static NodePropertyContract Property(
        string name,
        GraphValueType valueType,
        bool isRequired,
        string description,
        JsonNode? defaultValue = null,
        IReadOnlyList<string>? alternativeNames = null)
    {
        return new NodePropertyContract(name, valueType, isRequired, description, defaultValue, alternativeNames ?? []);
    }

    private static PortContract InExec()
    {
        return new PortContract("in", GraphPortKind.Execution, GraphValueType.None, true);
    }

    private static PortContract OutExec(string name)
    {
        return new PortContract(name, GraphPortKind.Execution, GraphValueType.None, false);
    }

    private static PortContract InData(string name, GraphValueType valueType)
    {
        return new PortContract(name, GraphPortKind.Data, valueType, true);
    }

    private static JsonNode? CreateEmptyValue(GraphValueType valueType)
    {
        return valueType switch
        {
            GraphValueType.Bool => false,
            GraphValueType.Int => 0,
            GraphValueType.Float => 0.0,
            GraphValueType.String or GraphValueType.Enum or GraphValueType.AssetReference => string.Empty,
            _ => null
        };
    }
}
