using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Pompo.Editor.Avalonia.ViewModels;

namespace Pompo.Editor.Avalonia.Controls;

public sealed class SceneStageView : Canvas
{
    public static readonly StyledProperty<ProjectWorkspaceViewModel?> WorkspaceProperty =
        AvaloniaProperty.Register<SceneStageView, ProjectWorkspaceViewModel?>(nameof(Workspace));

    private const double StageWidth = 640;
    private const double StageHeight = 360;
    private static readonly IBrush StageBackground = Brush.Parse("#111827");
    private static readonly IBrush StageBorder = Brush.Parse("#334155");
    private static readonly IBrush GuideBrush = Brush.Parse("#334155");
    private static readonly IBrush MutedBrush = Brush.Parse("#94a3b8");
    private static readonly IBrush TextBrush = Brushes.White;
    private static readonly IBrush CharacterBrush = Brush.Parse("#38bdf8");
    private static readonly IBrush SelectedCharacterBrush = Brush.Parse("#facc15");

    public SceneStageView()
    {
        Width = StageWidth;
        Height = StageHeight;
        MinHeight = 240;
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
        if (e.PropertyName is nameof(ProjectWorkspaceViewModel.SelectedScene) or
            nameof(ProjectWorkspaceViewModel.SceneBackgroundAssetId) or
            nameof(ProjectWorkspaceViewModel.SceneCharacterPlacements) or
            nameof(ProjectWorkspaceViewModel.SelectedScenePlacementId) or
            nameof(ProjectWorkspaceViewModel.ScenePlacementCharacterId) or
            nameof(ProjectWorkspaceViewModel.ScenePlacementExpressionId) or
            nameof(ProjectWorkspaceViewModel.ScenePlacementLayer) or
            nameof(ProjectWorkspaceViewModel.ScenePlacementX) or
            nameof(ProjectWorkspaceViewModel.ScenePlacementY))
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        Children.Clear();

        Children.Add(new Rectangle
        {
            Width = StageWidth,
            Height = StageHeight,
            Fill = StageBackground,
            Stroke = StageBorder,
            StrokeThickness = 1
        });

        DrawGuides();
        DrawHeader();

        if (Workspace?.SelectedScene is null)
        {
            AddText("No scene selected", 24, 88, TextBrush, 18, FontWeight.Bold);
            AddText("Select a scene to preview background and character placement.", 24, 118, MutedBrush, 13);
            return;
        }

        var placements = Workspace.SceneCharacterPlacements;
        if (placements.Count == 0)
        {
            AddText("No character placements", 24, 112, MutedBrush, 13);
            return;
        }

        foreach (var character in placements.OrderBy(item => item.Layer).ThenBy(item => item.PlacementId, StringComparer.Ordinal))
        {
            DrawCharacterMarker(character);
        }
    }

    private void DrawGuides()
    {
        for (var index = 1; index < 4; index++)
        {
            var x = StageWidth * index / 4;
            Children.Add(new Line
            {
                StartPoint = new Point(x, 56),
                EndPoint = new Point(x, StageHeight - 20),
                Stroke = GuideBrush,
                StrokeThickness = 1
            });
        }

        Children.Add(new Line
        {
            StartPoint = new Point(0, StageHeight - 44),
            EndPoint = new Point(StageWidth, StageHeight - 44),
            Stroke = Brush.Parse("#475569"),
            StrokeThickness = 1
        });
    }

    private void DrawHeader()
    {
        var title = Workspace?.SelectedScene?.DisplayName ?? "Scene Stage";
        var background = string.IsNullOrWhiteSpace(Workspace?.SceneBackgroundAssetId)
            ? "background: none"
            : $"background: {Workspace.SceneBackgroundAssetId}";
        AddText(title, 18, 16, TextBrush, 16, FontWeight.Bold);
        AddText(background, 18, 38, MutedBrush, 12);
        AddText("16:9 virtual canvas", StageWidth - 152, 20, MutedBrush, 12);
    }

    private void DrawCharacterMarker(SceneCharacterPlacementViewItem character)
    {
        var x = Math.Clamp(character.X, 0f, 1f) * (StageWidth - 96) + 48;
        var y = Math.Clamp(character.Y, 0f, 1.2f) / 1.2 * (StageHeight - 112) + 68;
        var markerBrush = character.IsSelected ? SelectedCharacterBrush : CharacterBrush;

        var body = new Border
        {
            Width = character.IsSelected ? 74 : 62,
            Height = character.IsSelected ? 112 : 96,
            Background = Brush.Parse(character.IsSelected ? "#facc1533" : "#38bdf833"),
            BorderBrush = markerBrush,
            BorderThickness = new Thickness(character.IsSelected ? 3 : 2),
            CornerRadius = new CornerRadius(6)
        };
        SetLeft(body, x - (body.Width / 2));
        SetTop(body, y - body.Height);
        Children.Add(body);

        var label = new Border
        {
            Background = Brush.Parse("#020617cc"),
            Padding = new Thickness(6, 3),
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock
            {
                Text = $"{character.CharacterId} {character.InitialExpressionId}".Trim(),
                Foreground = TextBrush,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 160
            }
        };
        SetLeft(label, Math.Clamp(x - 80, 8, StageWidth - 168));
        SetTop(label, Math.Clamp(y + 6, 64, StageHeight - 32));
        Children.Add(label);
    }

    private void AddText(
        string text,
        double x,
        double y,
        IBrush foreground,
        double fontSize,
        FontWeight fontWeight = default)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            Foreground = foreground,
            FontSize = fontSize,
            FontWeight = fontWeight,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = StageWidth - x - 12
        };
        SetLeft(textBlock, x);
        SetTop(textBlock, y);
        Children.Add(textBlock);
    }
}
