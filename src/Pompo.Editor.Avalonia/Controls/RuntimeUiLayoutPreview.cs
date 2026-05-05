using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Pompo.Editor.Avalonia.ViewModels;

namespace Pompo.Editor.Avalonia.Controls;

public sealed class RuntimeUiLayoutPreview : Canvas
{
    public static readonly StyledProperty<ProjectWorkspaceViewModel?> WorkspaceProperty =
        AvaloniaProperty.Register<RuntimeUiLayoutPreview, ProjectWorkspaceViewModel?>(nameof(Workspace));

    private const double PreviewWidth = 640;
    private const double PreviewHeight = 360;
    private const double VirtualWidth = 1920;
    private const double VirtualHeight = 1080;
    private static readonly IBrush CanvasBrush = Brush.Parse("#0f172a");
    private static readonly IBrush CanvasBorderBrush = Brush.Parse("#334155");
    private static readonly IBrush TextBrush = Brushes.White;
    private static readonly IBrush MutedBrush = Brush.Parse("#94a3b8");
    private static readonly IBrush DialogueBrush = Brush.Parse("#38bdf855");
    private static readonly IBrush DialogueBorder = Brush.Parse("#38bdf8");
    private static readonly IBrush NameBrush = Brush.Parse("#facc1555");
    private static readonly IBrush NameBorder = Brush.Parse("#facc15");
    private static readonly IBrush ChoiceBrush = Brush.Parse("#a78bfa55");
    private static readonly IBrush ChoiceBorder = Brush.Parse("#a78bfa");
    private static readonly IBrush SaveBrush = Brush.Parse("#34d39944");
    private static readonly IBrush SaveBorder = Brush.Parse("#34d399");
    private static readonly IBrush BacklogBrush = Brush.Parse("#fb718544");
    private static readonly IBrush BacklogBorder = Brush.Parse("#fb7185");
    private static readonly IBrush ErrorBrush = Brush.Parse("#ef4444");
    private DragOperation? _dragOperation;
    private Point _dragStartPoint;
    private VirtualRect _dragStartRect;

    public RuntimeUiLayoutPreview()
    {
        Width = PreviewWidth;
        Height = PreviewHeight;
        MinHeight = PreviewHeight;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
    }

    public ProjectWorkspaceViewModel? Workspace
    {
        get => GetValue(WorkspaceProperty);
        set => SetValue(WorkspaceProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WorkspaceProperty)
        {
            if (change.OldValue is ProjectWorkspaceViewModel oldWorkspace)
            {
                oldWorkspace.PropertyChanged -= WorkspaceChanged;
            }

            if (change.NewValue is ProjectWorkspaceViewModel newWorkspace)
            {
                newWorkspace.PropertyChanged += WorkspaceChanged;
            }

            Rebuild();
        }
    }

    private void WorkspaceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null ||
            e.PropertyName.StartsWith("RuntimeLayout", StringComparison.Ordinal) ||
            e.PropertyName.StartsWith("RuntimeAnimation", StringComparison.Ordinal))
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        Children.Clear();
        Children.Add(new Rectangle
        {
            Width = PreviewWidth,
            Height = PreviewHeight,
            Fill = CanvasBrush,
            Stroke = CanvasBorderBrush,
            StrokeThickness = 1
        });

        AddText("1920 x 1080 runtime UI layout", 16, 14, TextBrush, 14, FontWeight.Bold);
        AddText("Preview updates from Theme tab layout and animation fields.", 16, 36, MutedBrush, 12);
        DrawGuides();

        if (Workspace is null)
        {
            AddText("Open a project to preview layout.", 16, 74, MutedBrush, 13);
            return;
        }

        if (!TryReadRect(
                Workspace.RuntimeLayoutBacklogX,
                Workspace.RuntimeLayoutBacklogY,
                Workspace.RuntimeLayoutBacklogWidth,
                Workspace.RuntimeLayoutBacklogHeight,
                out var backlog))
        {
            AddText("Backlog rectangle has an invalid integer value.", 16, 74, ErrorBrush, 13);
        }
        else
        {
            DrawVirtualRect(backlog, BacklogBrush, BacklogBorder, "Backlog", DragTarget.Backlog, PanelOpacityPreview());
        }

        if (!TryReadRect(
                Workspace.RuntimeLayoutSaveMenuX,
                Workspace.RuntimeLayoutSaveMenuY,
                Workspace.RuntimeLayoutSaveMenuWidth,
                Workspace.RuntimeLayoutSaveMenuHeight,
                out var saveMenu))
        {
            AddText("Save menu rectangle has an invalid integer value.", 16, 92, ErrorBrush, 13);
        }
        else
        {
            DrawVirtualRect(saveMenu, SaveBrush, SaveBorder, "Save", DragTarget.SaveMenu, PanelOpacityPreview());
        }

        if (!TryReadRect(
                Workspace.RuntimeLayoutDialogueTextBoxX,
                Workspace.RuntimeLayoutDialogueTextBoxY,
                Workspace.RuntimeLayoutDialogueTextBoxWidth,
                Workspace.RuntimeLayoutDialogueTextBoxHeight,
                out var dialogue))
        {
            AddText("Dialogue text rectangle has an invalid integer value.", 16, 110, ErrorBrush, 13);
        }
        else
        {
            DrawVirtualRect(dialogue, DialogueBrush, DialogueBorder, "Dialogue", DragTarget.DialogueTextBox, PanelOpacityPreview());
        }

        if (!TryReadRect(
                Workspace.RuntimeLayoutDialogueNameBoxX,
                Workspace.RuntimeLayoutDialogueNameBoxY,
                Workspace.RuntimeLayoutDialogueNameBoxWidth,
                Workspace.RuntimeLayoutDialogueNameBoxHeight,
                out var nameBox))
        {
            AddText("Dialogue name rectangle has an invalid integer value.", 16, 128, ErrorBrush, 13);
        }
        else
        {
            DrawVirtualRect(nameBox, NameBrush, NameBorder, "Name", DragTarget.DialogueNameBox, PanelOpacityPreview());
        }

        if (TryReadInt(Workspace.RuntimeLayoutChoiceBoxWidth, out var choiceWidth) &&
            TryReadInt(Workspace.RuntimeLayoutChoiceBoxHeight, out var choiceHeight) &&
            TryReadInt(Workspace.RuntimeLayoutChoiceBoxSpacing, out var choiceSpacing))
        {
            DrawChoicePreview(choiceWidth, choiceHeight, choiceSpacing);
        }
        else
        {
            AddText("Choice dimensions have an invalid integer value.", 16, 146, ErrorBrush, 13);
        }

        AddAnimationLegend();
    }

    private static bool TryReadRect(
        string x,
        string y,
        string width,
        string height,
        out VirtualRect rect)
    {
        rect = default;
        if (!TryReadInt(x, out var parsedX) ||
            !TryReadInt(y, out var parsedY) ||
            !TryReadInt(width, out var parsedWidth) ||
            !TryReadInt(height, out var parsedHeight))
        {
            return false;
        }

        rect = new VirtualRect(parsedX, parsedY, parsedWidth, parsedHeight);
        return true;
    }

    private static bool TryReadInt(string value, out int parsed)
    {
        return int.TryParse(value.Trim(), out parsed);
    }

    private static bool TryReadFloat(string value, out float parsed)
    {
        return float.TryParse(
            value.Trim(),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out parsed);
    }

    private void DrawChoicePreview(int width, int height, int spacing)
    {
        var totalHeight = (height * 2) + spacing;
        var startX = (VirtualWidth - width) / 2;
        var startY = (VirtualHeight - totalHeight) / 2;
        DrawVirtualRect(new VirtualRect(startX, startY, width, height), ChoiceBrush, ChoiceBorder, "Choice", opacity: PanelOpacityPreview());
        DrawVirtualRect(
            new VirtualRect(startX, startY + height + spacing, width, height),
            ChoiceBrush,
            ChoiceBorder,
            "Selected",
            opacity: PanelOpacityPreview(),
            scale: SelectedChoiceScalePreview());
    }

    private void DrawVirtualRect(
        VirtualRect rect,
        IBrush fill,
        IBrush stroke,
        string label,
        DragTarget? dragTarget = null,
        double opacity = 1,
        double scale = 1)
    {
        var previewRect = ToPreviewRect(rect);
        var width = Math.Max(2, previewRect.Width * scale);
        var height = Math.Max(2, previewRect.Height * scale);
        var x = previewRect.Center.X - (width / 2);
        var y = previewRect.Center.Y - (height / 2);

        var shape = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = 2,
            Opacity = opacity,
            Cursor = dragTarget is null ? null : new Cursor(StandardCursorType.SizeAll)
        };
        SetLeft(shape, x);
        SetTop(shape, y);
        Children.Add(shape);

        if (dragTarget is not null)
        {
            var handle = new Rectangle
            {
                Width = 9,
                Height = 9,
                Fill = stroke,
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Opacity = opacity,
                Cursor = new Cursor(StandardCursorType.SizeAll)
            };
            SetLeft(handle, x + width - 9);
            SetTop(handle, y + height - 9);
            Children.Add(handle);
        }

        AddText(label, x + 5, y + 4, TextBrush, 11, FontWeight.SemiBold, Math.Max(48, width - 10));
    }

    private double PanelOpacityPreview()
    {
        if (Workspace is null || !Workspace.RuntimeAnimationEnabled)
        {
            return 1;
        }

        return TryReadInt(Workspace.RuntimeAnimationPanelFadeMilliseconds, out var fadeMilliseconds) && fadeMilliseconds > 0
            ? 0.5
            : 1;
    }

    private double SelectedChoiceScalePreview()
    {
        if (Workspace is null || !Workspace.RuntimeAnimationEnabled)
        {
            return 1;
        }

        if (!TryReadFloat(Workspace.RuntimeAnimationChoicePulseStrength, out var strength) ||
            strength <= 0)
        {
            return 1;
        }

        return 1 + Math.Clamp(strength, 0, 1);
    }

    private void AddAnimationLegend()
    {
        if (Workspace is null)
        {
            return;
        }

        var enabled = Workspace.RuntimeAnimationEnabled ? "on" : "off";
        var fade = TryReadInt(Workspace.RuntimeAnimationPanelFadeMilliseconds, out var fadeMs)
            ? $"{fadeMs}ms fade"
            : "invalid fade";
        var pulse = TryReadInt(Workspace.RuntimeAnimationChoicePulseMilliseconds, out var pulseMs) &&
            TryReadFloat(Workspace.RuntimeAnimationChoicePulseStrength, out var strength)
                ? $"{pulseMs}ms pulse x{1 + Math.Clamp(strength, 0, 1):0.00}"
                : "invalid pulse";
        var reveal = TryReadInt(Workspace.RuntimeAnimationTextRevealCharactersPerSecond, out var revealCps) && revealCps >= 0
            ? $"{revealCps} cps text"
            : "invalid text reveal";
        var playback = TryReadInt(Workspace.RuntimePlaybackAutoForwardDelayMilliseconds, out var autoMs) &&
            TryReadInt(Workspace.RuntimePlaybackSkipIntervalMilliseconds, out var skipMs) &&
            autoMs >= 0 &&
            skipMs >= 0
                ? $"{autoMs}ms auto, {skipMs}ms skip"
                : "invalid playback";
        AddText($"Animation preview: {enabled}, {fade}, {pulse}, {reveal}, {playback}", 16, PreviewHeight - 26, MutedBrush, 12);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (Workspace is null ||
            !args.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var position = args.GetPosition(this);
        if (!TryHitTarget(position, out var operation, out var rect))
        {
            return;
        }

        _dragOperation = operation;
        _dragStartPoint = position;
        _dragStartRect = rect;
        args.Pointer.Capture(this);
        args.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs args)
    {
        if (Workspace is null ||
            _dragOperation is null ||
            !args.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var position = args.GetPosition(this);
        var deltaVirtualX = (int)Math.Round((position.X - _dragStartPoint.X) / PreviewWidth * VirtualWidth);
        var deltaVirtualY = (int)Math.Round((position.Y - _dragStartPoint.Y) / PreviewHeight * VirtualHeight);
        if (_dragOperation.Value.Mode == DragMode.Move)
        {
            var x = (int)Math.Clamp(_dragStartRect.X + deltaVirtualX, 0, VirtualWidth - _dragStartRect.Width);
            var y = (int)Math.Clamp(_dragStartRect.Y + deltaVirtualY, 0, VirtualHeight - _dragStartRect.Height);
            ApplyDraggedRect(_dragOperation.Value.Target, _dragStartRect with { X = x, Y = y });
        }
        else
        {
            var width = (int)Math.Clamp(_dragStartRect.Width + deltaVirtualX, 24, VirtualWidth - _dragStartRect.X);
            var height = (int)Math.Clamp(_dragStartRect.Height + deltaVirtualY, 24, VirtualHeight - _dragStartRect.Y);
            ApplyDraggedRect(_dragOperation.Value.Target, _dragStartRect with { Width = width, Height = height });
        }

        args.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (_dragOperation is null)
        {
            return;
        }

        _dragOperation = null;
        args.Pointer.Capture(null);
        args.Handled = true;
    }

    private bool TryHitTarget(Point position, out DragOperation operation, out VirtualRect rect)
    {
        operation = default;
        rect = default;
        if (Workspace is null)
        {
            return false;
        }

        var candidates = new (DragTarget Target, VirtualRect Rect)[]
        {
            (DragTarget.DialogueNameBox, ReadRectOrDefault(
                Workspace.RuntimeLayoutDialogueNameBoxX,
                Workspace.RuntimeLayoutDialogueNameBoxY,
                Workspace.RuntimeLayoutDialogueNameBoxWidth,
                Workspace.RuntimeLayoutDialogueNameBoxHeight)),
            (DragTarget.DialogueTextBox, ReadRectOrDefault(
                Workspace.RuntimeLayoutDialogueTextBoxX,
                Workspace.RuntimeLayoutDialogueTextBoxY,
                Workspace.RuntimeLayoutDialogueTextBoxWidth,
                Workspace.RuntimeLayoutDialogueTextBoxHeight)),
            (DragTarget.SaveMenu, ReadRectOrDefault(
                Workspace.RuntimeLayoutSaveMenuX,
                Workspace.RuntimeLayoutSaveMenuY,
                Workspace.RuntimeLayoutSaveMenuWidth,
                Workspace.RuntimeLayoutSaveMenuHeight)),
            (DragTarget.Backlog, ReadRectOrDefault(
                Workspace.RuntimeLayoutBacklogX,
                Workspace.RuntimeLayoutBacklogY,
                Workspace.RuntimeLayoutBacklogWidth,
                Workspace.RuntimeLayoutBacklogHeight))
        };

        foreach (var candidate in candidates)
        {
            if (candidate.Rect.Width <= 0 ||
                candidate.Rect.Height <= 0 ||
                !ToPreviewRect(candidate.Rect).Contains(position))
            {
                continue;
            }

            operation = new DragOperation(
                candidate.Target,
                IsOnResizeHandle(position, candidate.Rect) ? DragMode.Resize : DragMode.Move);
            rect = candidate.Rect;
            return true;
        }

        return false;
    }

    private VirtualRect ReadRectOrDefault(
        string x,
        string y,
        string width,
        string height)
    {
        return TryReadRect(x, y, width, height, out var rect) ? rect : default;
    }

    private static Rect ToPreviewRect(VirtualRect rect)
    {
        return new Rect(
            rect.X / VirtualWidth * PreviewWidth,
            rect.Y / VirtualHeight * PreviewHeight,
            Math.Max(2, rect.Width / VirtualWidth * PreviewWidth),
            Math.Max(2, rect.Height / VirtualHeight * PreviewHeight));
    }

    private static bool IsOnResizeHandle(Point position, VirtualRect rect)
    {
        var previewRect = ToPreviewRect(rect);
        return position.X >= previewRect.Right - 14 &&
            position.X <= previewRect.Right + 4 &&
            position.Y >= previewRect.Bottom - 14 &&
            position.Y <= previewRect.Bottom + 4;
    }

    private void ApplyDraggedRect(DragTarget target, VirtualRect rect)
    {
        if (Workspace is null)
        {
            return;
        }

        switch (target)
        {
            case DragTarget.DialogueTextBox:
                Workspace.RuntimeLayoutDialogueTextBoxX = ((int)rect.X).ToString();
                Workspace.RuntimeLayoutDialogueTextBoxY = ((int)rect.Y).ToString();
                Workspace.RuntimeLayoutDialogueTextBoxWidth = ((int)rect.Width).ToString();
                Workspace.RuntimeLayoutDialogueTextBoxHeight = ((int)rect.Height).ToString();
                break;
            case DragTarget.DialogueNameBox:
                Workspace.RuntimeLayoutDialogueNameBoxX = ((int)rect.X).ToString();
                Workspace.RuntimeLayoutDialogueNameBoxY = ((int)rect.Y).ToString();
                Workspace.RuntimeLayoutDialogueNameBoxWidth = ((int)rect.Width).ToString();
                Workspace.RuntimeLayoutDialogueNameBoxHeight = ((int)rect.Height).ToString();
                break;
            case DragTarget.SaveMenu:
                Workspace.RuntimeLayoutSaveMenuX = ((int)rect.X).ToString();
                Workspace.RuntimeLayoutSaveMenuY = ((int)rect.Y).ToString();
                Workspace.RuntimeLayoutSaveMenuWidth = ((int)rect.Width).ToString();
                Workspace.RuntimeLayoutSaveMenuHeight = ((int)rect.Height).ToString();
                break;
            case DragTarget.Backlog:
                Workspace.RuntimeLayoutBacklogX = ((int)rect.X).ToString();
                Workspace.RuntimeLayoutBacklogY = ((int)rect.Y).ToString();
                Workspace.RuntimeLayoutBacklogWidth = ((int)rect.Width).ToString();
                Workspace.RuntimeLayoutBacklogHeight = ((int)rect.Height).ToString();
                break;
        }
    }

    private void DrawGuides()
    {
        for (var index = 1; index < 4; index++)
        {
            var x = PreviewWidth * index / 4;
            Children.Add(new Line
            {
                StartPoint = new Point(x, 0),
                EndPoint = new Point(x, PreviewHeight),
                Stroke = CanvasBorderBrush,
                StrokeThickness = 1
            });
        }

        for (var index = 1; index < 3; index++)
        {
            var y = PreviewHeight * index / 3;
            Children.Add(new Line
            {
                StartPoint = new Point(0, y),
                EndPoint = new Point(PreviewWidth, y),
                Stroke = CanvasBorderBrush,
                StrokeThickness = 1
            });
        }
    }

    private void AddText(
        string text,
        double x,
        double y,
        IBrush foreground,
        double fontSize,
        FontWeight fontWeight = default,
        double? maxWidth = null)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            Foreground = foreground,
            FontSize = fontSize,
            FontWeight = fontWeight,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = maxWidth ?? PreviewWidth - x - 12
        };
        SetLeft(textBlock, x);
        SetTop(textBlock, y);
        Children.Add(textBlock);
    }

    private readonly record struct VirtualRect(double X, double Y, double Width, double Height);

    private enum DragTarget
    {
        DialogueTextBox,
        DialogueNameBox,
        SaveMenu,
        Backlog
    }

    private readonly record struct DragOperation(DragTarget Target, DragMode Mode);

    private enum DragMode
    {
        Move,
        Resize
    }
}
