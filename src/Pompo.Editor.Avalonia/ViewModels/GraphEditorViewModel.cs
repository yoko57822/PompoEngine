using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pompo.Core.Graphs;
using Pompo.VisualScripting;
using Pompo.VisualScripting.Authoring;

namespace Pompo.Editor.Avalonia.ViewModels;

public sealed record GraphNodeViewItem(
    string NodeId,
    GraphNodeKind Kind,
    double X,
    double Y,
    bool IsSelected);

public sealed record GraphEdgeViewItem(
    string EdgeId,
    string FromNodeId,
    string FromPortId,
    string ToNodeId,
    string ToPortId);

public sealed record GraphCanvasNodeViewItem(
    string NodeId,
    GraphNodeKind Kind,
    double X,
    double Y,
    double Width,
    double Height,
    bool IsSelected);

public sealed record GraphCanvasEdgeViewItem(
    string EdgeId,
    double StartX,
    double StartY,
    double EndX,
    double EndY);

public sealed record GraphNodePropertyViewItem(
    string Name,
    GraphValueType ValueType,
    bool IsRequired,
    string Description,
    string DefaultValueJson,
    string AlternativeNames);

public sealed record CustomNodePaletteItem(
    string NodeType,
    string DisplayName,
    bool IsCondition,
    IReadOnlyDictionary<string, JsonNode?> DefaultProperties)
{
    public override string ToString()
    {
        return DisplayName;
    }
}

public sealed class GraphEditorViewModel : INotifyPropertyChanged
{
    private const double CanvasPadding = 28;
    private const double NodeWidth = 170;
    private const double NodeHeight = 72;
    private const double NodeScale = 0.42;

    private sealed record GraphEditorSnapshot(
        GraphDocument Graph,
        string? SelectedNodeId,
        string? ConnectionSourceNodeId);

    private readonly GraphAuthoringService _authoring;
    private readonly Stack<GraphEditorSnapshot> _undoStack = [];
    private readonly Stack<GraphEditorSnapshot> _redoStack = [];
    private GraphDocument _graph;
    private string? _selectedNodeId;
    private string? _connectionSourceNodeId;
    private string _selectedNodePropertiesJson = string.Empty;
    private string? _selectedNodePropertiesJsonError;
    private bool _isDirty;

    public GraphEditorViewModel(GraphDocument graph, GraphAuthoringService? authoring = null)
    {
        _graph = graph;
        _authoring = authoring ?? new GraphAuthoringService();
        Diagnostics = ToEditorDiagnostics(_authoring.Validate(graph).Diagnostics);
        RefreshSelectedNodePropertiesJson();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public GraphDocument Graph
    {
        get => _graph;
        private set
        {
            _graph = value;
            Diagnostics = ToEditorDiagnostics(_authoring.Validate(value).Diagnostics);
            RefreshSelectedNodePropertiesJson();
            OnPropertyChanged();
            OnPropertyChanged(nameof(GraphId));
            OnPropertyChanged(nameof(Nodes));
            OnPropertyChanged(nameof(Edges));
            OnPropertyChanged(nameof(CanvasNodes));
            OnPropertyChanged(nameof(CanvasEdges));
            OnPropertyChanged(nameof(CanvasWidth));
            OnPropertyChanged(nameof(CanvasHeight));
            OnPropertyChanged(nameof(SelectedNode));
            OnPropertyChanged(nameof(SelectedNodeKind));
            OnPropertyChanged(nameof(SelectedNodeText));
            OnPropertyChanged(nameof(SelectedNodePropertiesJson));
            OnPropertyChanged(nameof(SelectedNodePropertiesJsonError));
            OnPropertyChanged(nameof(SelectedNodePropertyHints));
            OnPropertyChanged(nameof(IsValid));
            OnPropertyChanged(nameof(Diagnostics));
        }
    }

    public string GraphId => Graph.GraphId;

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (_isDirty == value)
            {
                return;
            }

            _isDirty = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<EditorDiagnostic> Diagnostics { get; private set; }

    public bool IsValid => Diagnostics.Count == 0;

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public string? SelectedNodeId
    {
        get => _selectedNodeId;
        private set
        {
            if (_selectedNodeId == value)
            {
                return;
            }

            _selectedNodeId = value;
            RefreshSelectedNodePropertiesJson();
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedNode));
            OnPropertyChanged(nameof(SelectedNodeKind));
            OnPropertyChanged(nameof(SelectedNodeText));
            OnPropertyChanged(nameof(SelectedNodePropertiesJson));
            OnPropertyChanged(nameof(SelectedNodePropertiesJsonError));
            OnPropertyChanged(nameof(SelectedNodePropertyHints));
            OnPropertyChanged(nameof(Nodes));
            OnPropertyChanged(nameof(CanvasNodes));
            OnPropertyChanged(nameof(CanvasEdges));
        }
    }

    public string? ConnectionSourceNodeId
    {
        get => _connectionSourceNodeId;
        private set
        {
            if (_connectionSourceNodeId == value)
            {
                return;
            }

            _connectionSourceNodeId = value;
            OnPropertyChanged();
        }
    }

    public GraphNode? SelectedNode => SelectedNodeId is null
        ? null
        : Graph.Nodes.FirstOrDefault(node => string.Equals(node.NodeId, SelectedNodeId, StringComparison.Ordinal));

    public GraphNodeKind? SelectedNodeKind => SelectedNode?.Kind;

    public string SelectedNodeText
    {
        get => SelectedNode?.Properties.TryGetPropertyValue("text", out var value) == true &&
            value is not null &&
            value.GetValueKind() == JsonValueKind.String
                ? value.GetValue<string>()
                : string.Empty;
        set
        {
            if (SelectedNode is null || SelectedNodeText == value)
            {
                return;
            }

            var properties = SelectedNode.Properties.DeepClone().AsObject();
            properties["text"] = value;
            SetSelectedNodeProperties(properties);
        }
    }

    public string SelectedNodePropertiesJson
    {
        get => _selectedNodePropertiesJson;
        set
        {
            if (string.Equals(_selectedNodePropertiesJson, value, StringComparison.Ordinal))
            {
                return;
            }

            _selectedNodePropertiesJson = value;
            SelectedNodePropertiesJsonError = null;
            OnPropertyChanged();
        }
    }

    public string? SelectedNodePropertiesJsonError
    {
        get => _selectedNodePropertiesJsonError;
        private set
        {
            if (string.Equals(_selectedNodePropertiesJsonError, value, StringComparison.Ordinal))
            {
                return;
            }

            _selectedNodePropertiesJsonError = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<GraphNodePropertyViewItem> SelectedNodePropertyHints =>
        SelectedNodeKind is { } kind
            ? NodeCatalog.Find(kind)?.Properties
                .Select(property => new GraphNodePropertyViewItem(
                    property.Name,
                    property.ValueType,
                    property.IsRequired,
                    property.Description,
                    property.DefaultValue?.ToJsonString() ?? string.Empty,
                    string.Join(", ", property.AlternativeNames ?? [])))
                .ToArray() ?? []
            : [];

    public IReadOnlyList<GraphNodeViewItem> Nodes => Graph.Nodes
        .Select(node => new GraphNodeViewItem(
            node.NodeId,
            node.Kind,
            node.Position.X,
            node.Position.Y,
            string.Equals(node.NodeId, SelectedNodeId, StringComparison.Ordinal)))
        .ToArray();

    public IReadOnlyList<GraphEdgeViewItem> Edges => Graph.Edges
        .Select(edge => new GraphEdgeViewItem(edge.EdgeId, edge.FromNodeId, edge.FromPortId, edge.ToNodeId, edge.ToPortId))
        .ToArray();

    public IReadOnlyList<GraphCanvasNodeViewItem> CanvasNodes => Graph.Nodes
        .Select(node =>
        {
            var point = ToCanvasPoint(node.Position);
            return new GraphCanvasNodeViewItem(
                node.NodeId,
                node.Kind,
                point.X,
                point.Y,
                NodeWidth,
                NodeHeight,
                string.Equals(node.NodeId, SelectedNodeId, StringComparison.Ordinal));
        })
        .ToArray();

    public IReadOnlyList<GraphCanvasEdgeViewItem> CanvasEdges
    {
        get
        {
            var nodes = CanvasNodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
            return Graph.Edges
                .Where(edge => nodes.ContainsKey(edge.FromNodeId) && nodes.ContainsKey(edge.ToNodeId))
                .Select(edge =>
                {
                    var from = nodes[edge.FromNodeId];
                    var to = nodes[edge.ToNodeId];
                    return new GraphCanvasEdgeViewItem(
                        edge.EdgeId,
                        from.X + from.Width,
                        from.Y + (from.Height / 2),
                        to.X,
                        to.Y + (to.Height / 2));
                })
                .ToArray();
        }
    }

    public double CanvasWidth => Math.Max(640, CanvasNodes.Select(node => node.X + node.Width + CanvasPadding).DefaultIfEmpty(640).Max());

    public double CanvasHeight => Math.Max(260, CanvasNodes.Select(node => node.Y + node.Height + CanvasPadding).DefaultIfEmpty(260).Max());

    public void MoveNodeFromCanvas(string nodeId, double canvasX, double canvasY)
    {
        if (Graph.Nodes.All(node => !string.Equals(node.NodeId, nodeId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Node '{nodeId}' does not exist.");
        }

        SelectNode(nodeId);
        var graphPoint = FromCanvasPoint(canvasX, canvasY);
        MoveSelectedNode(graphPoint.X, graphPoint.Y);
    }

    public bool ActivateCanvasNode(string nodeId)
    {
        SelectNode(nodeId);
        if (ConnectionSourceNodeId is null)
        {
            MarkSelectedNodeAsConnectionSource();
            return false;
        }

        ConnectConnectionSourceToSelectedNode();
        ConnectionSourceNodeId = null;
        return true;
    }

    public void SelectNode(string? nodeId)
    {
        if (nodeId is not null && Graph.Nodes.All(node => !string.Equals(node.NodeId, nodeId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Node '{nodeId}' does not exist.");
        }

        SelectedNodeId = nodeId;
    }

    public void MarkSelectedNodeAsConnectionSource()
    {
        if (SelectedNode is null)
        {
            return;
        }

        if (FindFirstExecutionOutput(SelectedNode) is null)
        {
            throw new InvalidOperationException($"Node '{SelectedNode.NodeId}' has no execution output port.");
        }

        ConnectionSourceNodeId = SelectedNode.NodeId;
    }

    public void ConnectConnectionSourceToSelectedNode()
    {
        if (ConnectionSourceNodeId is null || SelectedNode is null)
        {
            return;
        }

        if (string.Equals(ConnectionSourceNodeId, SelectedNode.NodeId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Cannot connect a node to itself.");
        }

        var source = Graph.Nodes.FirstOrDefault(node => string.Equals(node.NodeId, ConnectionSourceNodeId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Node '{ConnectionSourceNodeId}' does not exist.");
        var fromPort = FindFirstExecutionOutput(source)
            ?? throw new InvalidOperationException($"Node '{source.NodeId}' has no execution output port.");
        var toPort = FindFirstExecutionInput(SelectedNode)
            ?? throw new InvalidOperationException($"Node '{SelectedNode.NodeId}' has no execution input port.");

        var edgeId = CreateUniqueEdgeId(source.NodeId, SelectedNode.NodeId);
        Connect(edgeId, source.NodeId, fromPort.PortId, SelectedNode.NodeId, toPort.PortId);
    }

    public void AddNode(GraphNodeKind kind, string nodeId, double x, double y)
    {
        var updatedGraph = _authoring.AddNode(Graph, kind, nodeId, new GraphPoint(x, y));
        PushUndoSnapshot();
        Graph = updatedGraph;
        SelectedNodeId = nodeId;
        IsDirty = true;
    }

    public void AddCustomNode(CustomNodePaletteItem item, string nodeId, double x, double y)
    {
        var properties = new JsonObject
        {
            ["nodeType"] = item.NodeType
        };
        foreach (var property in item.DefaultProperties)
        {
            properties[property.Key] = property.Value?.DeepClone();
        }

        var updatedGraph = _authoring.AddCustomNode(Graph, nodeId, new GraphPoint(x, y), properties, item.IsCondition);
        PushUndoSnapshot();
        Graph = updatedGraph;
        SelectedNodeId = nodeId;
        IsDirty = true;
    }

    public void MoveSelectedNode(double x, double y)
    {
        if (SelectedNodeId is null)
        {
            return;
        }

        var updatedGraph = _authoring.MoveNode(Graph, SelectedNodeId, new GraphPoint(x, y));
        PushUndoSnapshot();
        Graph = updatedGraph;
        IsDirty = true;
    }

    public void SetSelectedNodeProperties(JsonObject properties)
    {
        if (SelectedNodeId is null)
        {
            return;
        }

        var updatedGraph = _authoring.SetNodeProperties(Graph, SelectedNodeId, properties);
        PushUndoSnapshot();
        Graph = updatedGraph;
        IsDirty = true;
    }

    public bool ApplySelectedNodePropertiesJson()
    {
        if (SelectedNode is null)
        {
            SelectedNodePropertiesJsonError = "Select a node before applying properties.";
            return false;
        }

        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(SelectedNodePropertiesJson);
        }
        catch (JsonException exception)
        {
            SelectedNodePropertiesJsonError = $"Invalid JSON: {exception.Message}";
            return false;
        }

        if (parsed is not JsonObject properties)
        {
            SelectedNodePropertiesJsonError = "Node properties must be a JSON object.";
            return false;
        }

        SetSelectedNodeProperties(properties);
        SelectedNodePropertiesJsonError = null;
        return true;
    }

    public void Connect(string edgeId, string fromNodeId, string fromPortId, string toNodeId, string toPortId)
    {
        var updatedGraph = _authoring.Connect(Graph, edgeId, fromNodeId, fromPortId, toNodeId, toPortId);
        PushUndoSnapshot();
        Graph = updatedGraph;
        IsDirty = true;
    }

    public void Disconnect(string edgeId)
    {
        var updatedGraph = _authoring.Disconnect(Graph, edgeId);
        PushUndoSnapshot();
        Graph = updatedGraph;
        IsDirty = true;
    }

    public int DisconnectSelectedNodeEdges()
    {
        if (SelectedNodeId is null)
        {
            return 0;
        }

        var edgeIds = Graph.Edges
            .Where(edge =>
                string.Equals(edge.FromNodeId, SelectedNodeId, StringComparison.Ordinal) ||
                string.Equals(edge.ToNodeId, SelectedNodeId, StringComparison.Ordinal))
            .Select(edge => edge.EdgeId)
            .ToArray();

        if (edgeIds.Length == 0)
        {
            return 0;
        }

        var updatedGraph = Graph;
        foreach (var edgeId in edgeIds)
        {
            updatedGraph = _authoring.Disconnect(updatedGraph, edgeId);
        }

        PushUndoSnapshot();
        Graph = updatedGraph;
        IsDirty = true;
        return edgeIds.Length;
    }

    public bool DeleteSelectedNode()
    {
        if (SelectedNodeId is null)
        {
            return false;
        }

        var deletedNodeId = SelectedNodeId;
        var updatedGraph = _authoring.DeleteNode(Graph, deletedNodeId);
        PushUndoSnapshot();
        Graph = updatedGraph;
        SelectedNodeId = null;
        if (string.Equals(ConnectionSourceNodeId, deletedNodeId, StringComparison.Ordinal))
        {
            ConnectionSourceNodeId = null;
        }

        IsDirty = true;
        return true;
    }

    public string? DuplicateSelectedNode()
    {
        if (SelectedNode is null)
        {
            return null;
        }

        var source = SelectedNode;
        var duplicateNodeId = CreateDuplicateNodeId(source.NodeId);
        var updatedGraph = _authoring.AddNode(
            Graph,
            source.Kind,
            duplicateNodeId,
            new GraphPoint(source.Position.X + 80, source.Position.Y + 40),
            source.Properties.DeepClone().AsObject());

        PushUndoSnapshot();
        Graph = updatedGraph;
        SelectedNodeId = duplicateNodeId;
        ConnectionSourceNodeId = null;
        IsDirty = true;
        return duplicateNodeId;
    }

    public bool Undo()
    {
        if (_undoStack.Count == 0)
        {
            return false;
        }

        _redoStack.Push(CreateSnapshot());
        RestoreSnapshot(_undoStack.Pop());
        IsDirty = true;
        NotifyHistoryStateChanged();
        return true;
    }

    public bool Redo()
    {
        if (_redoStack.Count == 0)
        {
            return false;
        }

        _undoStack.Push(CreateSnapshot());
        RestoreSnapshot(_redoStack.Pop());
        IsDirty = true;
        NotifyHistoryStateChanged();
        return true;
    }

    public void MarkSaved()
    {
        IsDirty = false;
    }

    private void PushUndoSnapshot()
    {
        _undoStack.Push(CreateSnapshot());
        _redoStack.Clear();
        NotifyHistoryStateChanged();
    }

    private GraphEditorSnapshot CreateSnapshot()
    {
        return new GraphEditorSnapshot(Graph, SelectedNodeId, ConnectionSourceNodeId);
    }

    private void RestoreSnapshot(GraphEditorSnapshot snapshot)
    {
        Graph = snapshot.Graph;
        SelectedNodeId = snapshot.SelectedNodeId;
        ConnectionSourceNodeId = snapshot.ConnectionSourceNodeId;
    }

    private void RefreshSelectedNodePropertiesJson()
    {
        _selectedNodePropertiesJson = SelectedNode?.Properties.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        }) ?? string.Empty;
        SelectedNodePropertiesJsonError = null;
    }

    private void NotifyHistoryStateChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private static IReadOnlyList<EditorDiagnostic> ToEditorDiagnostics(IEnumerable<Pompo.VisualScripting.GraphDiagnostic> diagnostics)
    {
        return diagnostics
            .Select(diagnostic => new EditorDiagnostic(
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.NodeId,
                diagnostic.PortId))
            .ToArray();
    }

    private string CreateUniqueEdgeId(string fromNodeId, string toNodeId)
    {
        var index = Graph.Edges.Count + 1;
        string edgeId;
        do
        {
            edgeId = $"e_{SanitizeId(fromNodeId)}_{SanitizeId(toNodeId)}_{index++}";
        }
        while (Graph.Edges.Any(edge => string.Equals(edge.EdgeId, edgeId, StringComparison.Ordinal)));

        return edgeId;
    }

    private string CreateDuplicateNodeId(string sourceNodeId)
    {
        var prefix = $"{SanitizeId(sourceNodeId)}_copy";
        var index = 1;
        var candidate = prefix;
        while (Graph.Nodes.Any(node => string.Equals(node.NodeId, candidate, StringComparison.Ordinal)))
        {
            candidate = $"{prefix}_{++index}";
        }

        return candidate;
    }

    private static string SanitizeId(string value)
    {
        var sanitized = new string(value.Select(character =>
            char.IsLetterOrDigit(character) ? character : '_').ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "node" : sanitized;
    }

    private static GraphPort? FindFirstExecutionOutput(GraphNode node)
    {
        return node.Ports.FirstOrDefault(port => port.Kind == GraphPortKind.Execution && !port.IsInput);
    }

    private static GraphPort? FindFirstExecutionInput(GraphNode node)
    {
        return node.Ports.FirstOrDefault(port => port.Kind == GraphPortKind.Execution && port.IsInput);
    }

    private GraphPoint ToCanvasPoint(GraphPoint point)
    {
        var minX = Graph.Nodes.Select(node => node.Position.X).DefaultIfEmpty(0).Min();
        var minY = Graph.Nodes.Select(node => node.Position.Y).DefaultIfEmpty(0).Min();
        return new GraphPoint(
            CanvasPadding + ((point.X - minX) * NodeScale),
            CanvasPadding + ((point.Y - minY) * NodeScale));
    }

    private GraphPoint FromCanvasPoint(double canvasX, double canvasY)
    {
        var selectedNode = SelectedNode ?? throw new InvalidOperationException("Select a node before moving it.");
        var selectedCanvasPoint = ToCanvasPoint(selectedNode.Position);
        var deltaX = (canvasX - selectedCanvasPoint.X) / NodeScale;
        var deltaY = (canvasY - selectedCanvasPoint.Y) / NodeScale;
        return new GraphPoint(
            Math.Round(selectedNode.Position.X + deltaX, 2),
            Math.Round(selectedNode.Position.Y + deltaY, 2));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
