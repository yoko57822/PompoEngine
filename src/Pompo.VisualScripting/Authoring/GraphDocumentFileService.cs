using System.Text.Json;
using Pompo.Core.Graphs;
using Pompo.Core.Project;

namespace Pompo.VisualScripting.Authoring;

public sealed class GraphDocumentFileService
{
    public async Task SaveAsync(
        string graphPath,
        GraphDocument graph,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(graphPath);
        await AtomicFileWriter.WriteJsonAsync(
            graphPath,
            graph,
            ProjectFileService.CreateJsonOptions(),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<GraphDocument> LoadAsync(
        string graphPath,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(graphPath);
        var graph = await JsonSerializer.DeserializeAsync<GraphDocument>(
            stream,
            ProjectFileService.CreateJsonOptions(),
            cancellationToken).ConfigureAwait(false);
        return graph ?? throw new InvalidDataException($"Graph file '{graphPath}' is empty or invalid.");
    }
}
