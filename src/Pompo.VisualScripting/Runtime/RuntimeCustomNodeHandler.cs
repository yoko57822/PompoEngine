using System.Text.Json.Nodes;

namespace Pompo.VisualScripting.Runtime;

public sealed record RuntimeCustomNodeContext(
    string GraphId,
    string SourceNodeId,
    IReadOnlyDictionary<string, JsonNode?> Arguments,
    IDictionary<string, object?> Variables);

public sealed record RuntimeCustomNodeResult(string? OutputPort = "out");

public interface IRuntimeCustomNodeHandler
{
    ValueTask<RuntimeCustomNodeResult> ExecuteAsync(
        RuntimeCustomNodeContext context,
        CancellationToken cancellationToken = default);
}
