using Pompo.Core.Graphs;
using System.Text.Json.Nodes;

namespace Pompo.VisualScripting;

internal static class GraphTraversal
{
    public static IReadOnlyList<GraphNode> GetReachableExecutionOrder(GraphDocument graph)
    {
        var nodes = graph.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var start = graph.Nodes.FirstOrDefault(node => node.Kind == GraphNodeKind.Start);
        if (start is null)
        {
            return [];
        }

        var ordered = new List<GraphNode>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<GraphNode>();
        stack.Push(start);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (!visited.Add(node.NodeId))
            {
                continue;
            }

            ordered.Add(node);

            var next = graph.Edges
                .Where(edge => edge.FromNodeId == node.NodeId)
                .Select(edge => nodes.TryGetValue(edge.ToNodeId, out var target) ? target : null)
                .Where(target => target is not null)
                .Concat(GetImplicitTargets(node, nodes))
                .Reverse();

            foreach (var target in next)
            {
                stack.Push(target!);
            }
        }

        return ordered;
    }

    private static IEnumerable<GraphNode> GetImplicitTargets(
        GraphNode node,
        IReadOnlyDictionary<string, GraphNode> nodes)
    {
        if (node.Kind != GraphNodeKind.Jump)
        {
            yield break;
        }

        var targetNodeId = ReadString(node, "targetNodeId") ?? ReadString(node, "nodeId");
        if (!string.IsNullOrWhiteSpace(targetNodeId) &&
            nodes.TryGetValue(targetNodeId, out var target))
        {
            yield return target;
        }
    }

    private static string? ReadString(GraphNode node, string propertyName)
    {
        return node.Properties.TryGetPropertyValue(propertyName, out var value) && value is JsonValue jsonValue
            ? jsonValue.GetValue<string>()
            : null;
    }
}
