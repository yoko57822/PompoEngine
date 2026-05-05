using System.Text.Json;
using System.Text.Json.Nodes;
using Pompo.Core.Graphs;

namespace Pompo.VisualScripting;

public sealed record GraphDiagnostic(string Code, string Message, string? NodeId = null, string? PortId = null);

public sealed record GraphValidationResult(IReadOnlyList<GraphDiagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;
}

public sealed class GraphValidator
{
    public GraphValidationResult Validate(GraphDocument graph)
    {
        var diagnostics = new List<GraphDiagnostic>();
        var nodes = graph.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var ports = graph.Nodes
            .SelectMany(node => node.Ports.Select(port => (node.NodeId, Port: port)))
            .ToDictionary(item => (item.NodeId, item.Port.PortId), item => item.Port);

        var startNodes = graph.Nodes.Where(node => node.Kind == GraphNodeKind.Start).ToArray();
        if (startNodes.Length == 0)
        {
            diagnostics.Add(new GraphDiagnostic("GRAPH001", "Graph must contain a Start node."));
        }
        else if (startNodes.Length > 1)
        {
            diagnostics.Add(new GraphDiagnostic("GRAPH002", "Graph can contain only one Start node."));
        }

        foreach (var node in graph.Nodes)
        {
            var definition = NodeCatalog.Find(node.Kind);
            if (definition is null && node.Kind != GraphNodeKind.Custom)
            {
                diagnostics.Add(new GraphDiagnostic("GRAPH003", $"Node kind '{node.Kind}' is not registered.", node.NodeId));
                continue;
            }

            if (definition is not null)
            {
                ValidateRequiredPorts(node, definition, diagnostics);
                ValidateRequiredProperties(node, definition, diagnostics);
            }

            if (node.Kind == GraphNodeKind.Choice)
            {
                ValidateChoicePorts(node, graph.Edges, diagnostics);
            }
        }

        foreach (var edge in graph.Edges)
        {
            if (!nodes.ContainsKey(edge.FromNodeId))
            {
                diagnostics.Add(new GraphDiagnostic("GRAPH004", $"Edge '{edge.EdgeId}' starts at missing node '{edge.FromNodeId}'."));
                continue;
            }

            if (!nodes.ContainsKey(edge.ToNodeId))
            {
                diagnostics.Add(new GraphDiagnostic("GRAPH005", $"Edge '{edge.EdgeId}' targets missing node '{edge.ToNodeId}'."));
                continue;
            }

            if (!ports.TryGetValue((edge.FromNodeId, edge.FromPortId), out var fromPort))
            {
                diagnostics.Add(new GraphDiagnostic("GRAPH006", $"Edge '{edge.EdgeId}' starts at missing port '{edge.FromPortId}'.", edge.FromNodeId, edge.FromPortId));
                continue;
            }

            if (!ports.TryGetValue((edge.ToNodeId, edge.ToPortId), out var toPort))
            {
                diagnostics.Add(new GraphDiagnostic("GRAPH007", $"Edge '{edge.EdgeId}' targets missing port '{edge.ToPortId}'.", edge.ToNodeId, edge.ToPortId));
                continue;
            }

            if (fromPort.Kind != toPort.Kind)
            {
                diagnostics.Add(new GraphDiagnostic("GRAPH008", $"Edge '{edge.EdgeId}' mixes execution and data ports.", edge.FromNodeId, edge.FromPortId));
            }

            if (fromPort.IsInput || !toPort.IsInput)
            {
                diagnostics.Add(new GraphDiagnostic("GRAPH009", $"Edge '{edge.EdgeId}' direction is invalid.", edge.FromNodeId, edge.FromPortId));
            }

            if (fromPort.Kind == GraphPortKind.Data && fromPort.ValueType != toPort.ValueType)
            {
                diagnostics.Add(new GraphDiagnostic("GRAPH010", $"Edge '{edge.EdgeId}' has data type mismatch: '{fromPort.ValueType}' -> '{toPort.ValueType}'.", edge.FromNodeId, edge.FromPortId));
            }
        }

        foreach (var unreachable in FindUnreachableNodes(graph))
        {
            diagnostics.Add(new GraphDiagnostic("GRAPH011", $"Node '{unreachable.NodeId}' is unreachable from Start.", unreachable.NodeId));
        }

        return new GraphValidationResult(diagnostics);
    }

    private static void ValidateRequiredPorts(
        GraphNode node,
        NodeDefinition definition,
        ICollection<GraphDiagnostic> diagnostics)
    {
        foreach (var requiredPort in definition.RequiredPorts)
        {
            var found = node.Ports.Any(port =>
                port.Name == requiredPort.Name &&
                port.Kind == requiredPort.Kind &&
                port.ValueType == requiredPort.ValueType &&
                port.IsInput == requiredPort.IsInput);

            if (!found)
            {
                diagnostics.Add(new GraphDiagnostic(
                    "GRAPH012",
                    $"Node '{node.NodeId}' is missing required port '{requiredPort.Name}'.",
                    node.NodeId));
            }
        }
    }

    private static void ValidateRequiredProperties(
        GraphNode node,
        NodeDefinition definition,
        ICollection<GraphDiagnostic> diagnostics)
    {
        foreach (var property in definition.Properties.Where(property => property.IsRequired))
        {
            if (!TryReadProperty(node.Properties, property.Name, property.AlternativeNames ?? [], out var value) ||
                IsMissingRequiredValue(value, property.ValueType))
            {
                diagnostics.Add(new GraphDiagnostic(
                    "GRAPH013",
                    $"Node '{node.NodeId}' is missing required property '{property.Name}'.",
                    node.NodeId));
            }
        }
    }

    private static void ValidateChoicePorts(
        GraphNode node,
        IReadOnlyList<GraphEdge> edges,
        ICollection<GraphDiagnostic> diagnostics)
    {
        if (!node.Properties.TryGetPropertyValue("choices", out var choicesNode) ||
            choicesNode is not JsonArray choices)
        {
            return;
        }

        var outputPorts = node.Ports
            .Where(port => port.Kind == GraphPortKind.Execution && !port.IsInput)
            .Select(port => port.PortId)
            .ToHashSet(StringComparer.Ordinal);
        var connectedPorts = edges
            .Where(edge => string.Equals(edge.FromNodeId, node.NodeId, StringComparison.Ordinal))
            .Select(edge => edge.FromPortId)
            .ToHashSet(StringComparer.Ordinal);

        var validChoiceObjects = new List<JsonObject>();
        for (var index = 0; index < choices.Count; index++)
        {
            if (choices[index] is not JsonObject choice)
            {
                diagnostics.Add(new GraphDiagnostic(
                    "GRAPH016",
                    $"Choice node '{node.NodeId}' has a malformed choice entry at index {index}.",
                    node.NodeId));
                continue;
            }

            validChoiceObjects.Add(choice);
            var portId = ReadChoicePort(choice);
            if (string.IsNullOrWhiteSpace(portId) ||
                !outputPorts.Contains(portId) ||
                !connectedPorts.Contains(portId))
            {
                diagnostics.Add(new GraphDiagnostic(
                    "GRAPH014",
                    $"Choice node '{node.NodeId}' references missing or disconnected output port '{portId ?? "<invalid>"}'.",
                    node.NodeId,
                    portId));
            }
        }

        if (validChoiceObjects.Count > 0 &&
            validChoiceObjects.All(IsStaticallyDisabledChoice))
        {
            diagnostics.Add(new GraphDiagnostic(
                "GRAPH015",
                $"Choice node '{node.NodeId}' has no statically enabled choices.",
                node.NodeId));
        }
    }

    private static string? ReadChoicePort(JsonObject choice)
    {
        if (!choice.TryGetPropertyValue("port", out var portNode) ||
            portNode is null)
        {
            return "choice";
        }

        return portNode.GetValueKind() == JsonValueKind.String
            ? portNode.GetValue<string>()
            : null;
    }

    private static bool IsStaticallyDisabledChoice(JsonObject choice)
    {
        return choice.TryGetPropertyValue("enabled", out var enabledNode) &&
            enabledNode?.GetValueKind() == JsonValueKind.False &&
            !choice.TryGetPropertyValue("enabledVariable", out _);
    }

    private static bool TryReadProperty(
        JsonObject properties,
        string propertyName,
        IReadOnlyList<string> alternativeNames,
        out JsonNode? value)
    {
        if (properties.TryGetPropertyValue(propertyName, out value))
        {
            return true;
        }

        foreach (var alternativeName in alternativeNames)
        {
            if (properties.TryGetPropertyValue(alternativeName, out value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMissingRequiredValue(JsonNode? value, GraphValueType valueType)
    {
        if (value is null)
        {
            return true;
        }

        if (valueType is GraphValueType.String or GraphValueType.Enum or GraphValueType.AssetReference)
        {
            return value.GetValueKind() != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(value.GetValue<string>());
        }

        return valueType switch
        {
            GraphValueType.Bool => value.GetValueKind() is not JsonValueKind.True and not JsonValueKind.False,
            GraphValueType.Int or GraphValueType.Float => value.GetValueKind() != JsonValueKind.Number,
            _ => false
        };
    }

    private static IEnumerable<GraphNode> FindUnreachableNodes(GraphDocument graph)
    {
        var reachable = GraphTraversal.GetReachableExecutionOrder(graph)
            .Select(node => node.NodeId)
            .ToHashSet(StringComparer.Ordinal);

        return graph.Nodes.Where(node => node.Kind != GraphNodeKind.Start && !reachable.Contains(node.NodeId));
    }
}
