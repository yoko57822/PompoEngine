using System.Text.Json.Nodes;
using Pompo.Core;
using Pompo.Core.Graphs;

namespace Pompo.VisualScripting.Authoring;

public sealed class GraphAuthoringService
{
    public GraphDocument CreateEmpty(string graphId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(graphId);
        return new GraphDocument(ProjectConstants.CurrentSchemaVersion, graphId, [], []);
    }

    public GraphDocument AddNode(
        GraphDocument graph,
        GraphNodeKind kind,
        string nodeId,
        GraphPoint position,
        JsonObject? properties = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        if (graph.Nodes.Any(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Node '{nodeId}' already exists.");
        }

        if (kind == GraphNodeKind.Start &&
            graph.Nodes.Any(node => node.Kind == GraphNodeKind.Start))
        {
            throw new InvalidOperationException("Graph can contain only one Start node.");
        }

        var node = new GraphNode(
            nodeId,
            kind,
            position,
            CreateDefaultPorts(kind),
            properties?.DeepClone().AsObject() ?? NodeCatalog.CreateDefaultProperties(kind));

        return graph with { Nodes = graph.Nodes.Concat([node]).ToArray() };
    }

    public GraphDocument AddCustomNode(
        GraphDocument graph,
        string nodeId,
        GraphPoint position,
        JsonObject properties,
        bool isCondition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        if (graph.Nodes.Any(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Node '{nodeId}' already exists.");
        }

        var node = new GraphNode(
            nodeId,
            GraphNodeKind.Custom,
            position,
            isCondition
                ?
                [
                    NodeCatalog.InExecPort(),
                    NodeCatalog.OutExecPort("true", "true"),
                    NodeCatalog.OutExecPort("false", "false")
                ]
                : [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            properties.DeepClone().AsObject());

        return graph with { Nodes = graph.Nodes.Concat([node]).ToArray() };
    }

    public GraphDocument MoveNode(GraphDocument graph, string nodeId, GraphPoint position)
    {
        var found = false;
        var nodes = graph.Nodes.Select(node =>
        {
            if (!string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
            {
                return node;
            }

            found = true;
            return node with { Position = position };
        }).ToArray();

        if (!found)
        {
            throw new InvalidOperationException($"Node '{nodeId}' does not exist.");
        }

        return graph with { Nodes = nodes };
    }

    public GraphDocument SetNodeProperties(GraphDocument graph, string nodeId, JsonObject properties)
    {
        var found = false;
        var nodes = graph.Nodes.Select(node =>
        {
            if (!string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
            {
                return node;
            }

            found = true;
            return node with { Properties = properties.DeepClone().AsObject() };
        }).ToArray();

        if (!found)
        {
            throw new InvalidOperationException($"Node '{nodeId}' does not exist.");
        }

        return graph with { Nodes = nodes };
    }

    public GraphDocument DeleteNode(GraphDocument graph, string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        var nodes = graph.Nodes
            .Where(node => !string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
            .ToArray();
        if (nodes.Length == graph.Nodes.Count)
        {
            throw new InvalidOperationException($"Node '{nodeId}' does not exist.");
        }

        var edges = graph.Edges
            .Where(edge =>
                !string.Equals(edge.FromNodeId, nodeId, StringComparison.Ordinal) &&
                !string.Equals(edge.ToNodeId, nodeId, StringComparison.Ordinal))
            .ToArray();
        return graph with
        {
            Nodes = nodes,
            Edges = edges
        };
    }

    public GraphDocument Connect(
        GraphDocument graph,
        string edgeId,
        string fromNodeId,
        string fromPortId,
        string toNodeId,
        string toPortId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(edgeId);
        if (graph.Edges.Any(edge => string.Equals(edge.EdgeId, edgeId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Edge '{edgeId}' already exists.");
        }

        var edge = new GraphEdge(edgeId, fromNodeId, fromPortId, toNodeId, toPortId);
        var candidate = graph with { Edges = graph.Edges.Concat([edge]).ToArray() };
        var diagnostics = new GraphValidator().Validate(candidate).Diagnostics
            .Where(diagnostic => diagnostic.Code is "GRAPH004" or "GRAPH005" or "GRAPH006" or "GRAPH007" or "GRAPH008" or "GRAPH009" or "GRAPH010")
            .ToArray();

        if (diagnostics.Length > 0)
        {
            throw new InvalidOperationException(diagnostics[0].Message);
        }

        return candidate;
    }

    public GraphDocument Disconnect(GraphDocument graph, string edgeId)
    {
        var edges = graph.Edges
            .Where(edge => !string.Equals(edge.EdgeId, edgeId, StringComparison.Ordinal))
            .ToArray();
        if (edges.Length == graph.Edges.Count)
        {
            throw new InvalidOperationException($"Edge '{edgeId}' does not exist.");
        }

        return graph with { Edges = edges };
    }

    public GraphValidationResult Validate(GraphDocument graph)
    {
        return new GraphValidator().Validate(graph);
    }

    public static IReadOnlyList<GraphPort> CreateDefaultPorts(GraphNodeKind kind)
    {
        return kind switch
        {
            GraphNodeKind.Start => [NodeCatalog.OutExecPort()],
            GraphNodeKind.Branch =>
            [
                NodeCatalog.InExecPort(),
                NodeCatalog.OutExecPort("true", "true"),
                NodeCatalog.OutExecPort("false", "false"),
                new GraphPort("condition", "condition", GraphPortKind.Data, GraphValueType.Bool, true)
            ],
            GraphNodeKind.Choice =>
            [
                NodeCatalog.InExecPort(),
                NodeCatalog.OutExecPort("left", "left"),
                NodeCatalog.OutExecPort("right", "right")
            ],
            GraphNodeKind.Return or GraphNodeKind.EndScene or GraphNodeKind.End => [NodeCatalog.InExecPort()],
            _ => [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()]
        };
    }
}
