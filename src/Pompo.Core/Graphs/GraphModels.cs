using System.Text.Json.Nodes;

namespace Pompo.Core.Graphs;

public enum GraphNodeKind
{
    Start,
    Dialogue,
    Narration,
    Choice,
    SetVariable,
    Branch,
    Jump,
    CallGraph,
    Return,
    ShowCharacter,
    HideCharacter,
    MoveCharacter,
    ChangeExpression,
    PlayBgm,
    StopBgm,
    PlaySfx,
    PlayVoice,
    StopVoice,
    ChangeBackground,
    Fade,
    Wait,
    SavePoint,
    UnlockCg,
    EndScene,
    End,
    Custom
}

public enum GraphPortKind
{
    Execution,
    Data
}

public enum GraphValueType
{
    None,
    Bool,
    Int,
    Float,
    String,
    Enum,
    AssetReference
}

public sealed record GraphDocument(
    int Version,
    string GraphId,
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges,
    string? CompiledCacheHash = null);

public sealed record GraphNode(
    string NodeId,
    GraphNodeKind Kind,
    GraphPoint Position,
    IReadOnlyList<GraphPort> Ports,
    JsonObject Properties);

public sealed record GraphPort(
    string PortId,
    string Name,
    GraphPortKind Kind,
    GraphValueType ValueType,
    bool IsInput);

public sealed record GraphEdge(
    string EdgeId,
    string FromNodeId,
    string FromPortId,
    string ToNodeId,
    string ToPortId);

public sealed record GraphPoint(double X, double Y);
