using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Pompo.Editor.Avalonia.ViewModels;

namespace Pompo.Editor.Avalonia.Controls;

public sealed class GraphCanvasView : Canvas
{
    public static readonly StyledProperty<GraphEditorViewModel?> GraphEditorProperty =
        AvaloniaProperty.Register<GraphCanvasView, GraphEditorViewModel?>(nameof(GraphEditor));

    private static readonly IBrush NodeBackground = Brush.Parse("#ffffff");
    private static readonly IBrush SelectedNodeBackground = Brush.Parse("#eff6ff");
    private static readonly IBrush NodeBorder = Brush.Parse("#cbd5e1");
    private static readonly IBrush SelectedNodeBorder = Brush.Parse("#2563eb");
    private static readonly IBrush EdgeBrush = Brush.Parse("#94a3b8");
    private static readonly IBrush TextBrush = Brush.Parse("#111827");
    private static readonly IBrush MutedTextBrush = Brush.Parse("#64748b");
    private string? _draggingNodeId;
    private Vector _dragOffset;
    private bool _dragMoved;

    public GraphEditorViewModel? GraphEditor
    {
        get => GetValue(GraphEditorProperty);
        set => SetValue(GraphEditorProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == GraphEditorProperty)
        {
            if (change.OldValue is GraphEditorViewModel oldEditor)
            {
                oldEditor.PropertyChanged -= GraphEditorChanged;
            }

            if (change.NewValue is GraphEditorViewModel newEditor)
            {
                newEditor.PropertyChanged += GraphEditorChanged;
            }

            Rebuild();
        }
    }

    private void GraphEditorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GraphEditorViewModel.CanvasNodes) or
            nameof(GraphEditorViewModel.CanvasEdges) or
            nameof(GraphEditorViewModel.CanvasWidth) or
            nameof(GraphEditorViewModel.CanvasHeight))
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        Children.Clear();

        if (GraphEditor is null)
        {
            Width = 640;
            Height = 260;
            return;
        }

        Width = GraphEditor.CanvasWidth;
        Height = GraphEditor.CanvasHeight;
        foreach (var edge in GraphEditor.CanvasEdges)
        {
            Children.Add(new Line
            {
                StartPoint = new Point(edge.StartX, edge.StartY),
                EndPoint = new Point(edge.EndX, edge.EndY),
                Stroke = EdgeBrush,
                StrokeThickness = 2
            });
        }

        foreach (var node in GraphEditor.CanvasNodes)
        {
            var card = CreateNodeCard(node);
            SetLeft(card, node.X);
            SetTop(card, node.Y);
            Children.Add(card);
        }
    }

    private Control CreateNodeCard(GraphCanvasNodeViewItem node)
    {
        var card = new Border
        {
            Width = node.Width,
            Height = node.Height,
            Background = node.IsSelected ? SelectedNodeBackground : NodeBackground,
            BorderBrush = node.IsSelected ? SelectedNodeBorder : NodeBorder,
            BorderThickness = new Thickness(node.IsSelected ? 2 : 1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(10, 8),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = node.NodeId,
                        Foreground = TextBrush,
                        FontWeight = FontWeight.Bold,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    },
                    new TextBlock
                    {
                        Text = node.Kind.ToString(),
                        Foreground = MutedTextBrush,
                        FontSize = 12,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    }
                }
            }
        };

        card.PointerPressed += (sender, args) =>
        {
            GraphEditor?.SelectNode(node.NodeId);
            _draggingNodeId = node.NodeId;
            _dragOffset = args.GetPosition(this) - new Point(node.X, node.Y);
            _dragMoved = false;
            if (sender is InputElement input)
            {
                args.Pointer.Capture(input);
            }

            args.Handled = true;
        };
        card.PointerMoved += (_, args) =>
        {
            if (_draggingNodeId is null ||
                !args.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            var position = args.GetPosition(this) - _dragOffset;
            _dragMoved = true;
            GraphEditor?.MoveNodeFromCanvas(_draggingNodeId, Math.Max(0, position.X), Math.Max(0, position.Y));
            args.Handled = true;
        };
        card.PointerReleased += (_, args) =>
        {
            if (!_dragMoved)
            {
                GraphEditor?.ActivateCanvasNode(node.NodeId);
            }

            _draggingNodeId = null;
            _dragMoved = false;
            args.Pointer.Capture(null);
            args.Handled = true;
        };
        return card;
    }
}
