using System.Text.Json.Nodes;
using Pompo.Core.Graphs;

namespace Pompo.VisualScripting;

public sealed record PompoGraphIR(string GraphId, IReadOnlyList<PompoIRInstruction> Instructions);

public sealed record PompoIRInstruction(
    int Index,
    string SourceNodeId,
    GraphNodeKind Operation,
    IReadOnlyDictionary<string, JsonNode?> Arguments,
    IReadOnlyDictionary<string, int> Jumps);

public sealed class GraphCompiler
{
    private readonly GraphValidator _validator = new();

    public PompoGraphIR Compile(GraphDocument graph)
    {
        var validation = _validator.Validate(graph);
        if (!validation.IsValid)
        {
            throw new GraphCompilationException(validation.Diagnostics);
        }

        var ordered = GraphTraversal.GetReachableExecutionOrder(graph);
        var indexByNode = ordered
            .Select((node, index) => (node.NodeId, index))
            .ToDictionary(item => item.NodeId, item => item.index, StringComparer.Ordinal);

        var instructions = ordered.Select((node, index) =>
        {
            var jumps = graph.Edges
                .Where(edge => edge.FromNodeId == node.NodeId)
                .Select(edge => (edge.FromPortId, edge.ToNodeId))
                .Where(edge => indexByNode.ContainsKey(edge.ToNodeId))
                .ToDictionary(edge => edge.FromPortId, edge => indexByNode[edge.ToNodeId], StringComparer.Ordinal);

            var args = node.Properties.ToDictionary(
                property => property.Key,
                property => property.Value?.DeepClone(),
                StringComparer.Ordinal);

            return new PompoIRInstruction(index, node.NodeId, node.Kind, args, jumps);
        }).ToArray();

        return new PompoGraphIR(graph.GraphId, instructions);
    }
}

public sealed class GraphCompilationException(IReadOnlyList<GraphDiagnostic> diagnostics)
    : InvalidOperationException("Graph cannot be compiled.")
{
    public IReadOnlyList<GraphDiagnostic> Diagnostics { get; } = diagnostics;
}
