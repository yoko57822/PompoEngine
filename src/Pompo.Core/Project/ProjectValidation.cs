using System.Text.Json;
using System.Text.Json.Nodes;
using Pompo.Core.Assets;
using Pompo.Core.Characters;
using Pompo.Core.Graphs;
using Pompo.Core.Localization;

namespace Pompo.Core.Project;

public sealed record ProjectDiagnostic(string Code, string Message, string? DocumentPath = null, string? ElementId = null);

public sealed record ProjectValidationResult(IReadOnlyList<ProjectDiagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;
}

public sealed class ProjectValidator
{
    public ProjectValidationResult Validate(PompoProjectDocument document, string? projectRoot = null)
    {
        var diagnostics = new List<ProjectDiagnostic>();

        if (document.SchemaVersion > ProjectConstants.CurrentSchemaVersion)
        {
            diagnostics.Add(new ProjectDiagnostic(
                "POMPO001",
                $"Project schema {document.SchemaVersion} is newer than supported schema {ProjectConstants.CurrentSchemaVersion}."));
        }

        foreach (var scene in document.Scenes)
        {
            if (!string.IsNullOrWhiteSpace(scene.StartGraphId) &&
                document.Graphs.All(graph => graph.GraphId != scene.StartGraphId))
            {
                diagnostics.Add(new ProjectDiagnostic(
                    "POMPO002",
                    $"Scene '{scene.SceneId}' references missing start graph '{scene.StartGraphId}'.",
                    scene.SceneId));
            }

            foreach (var layer in scene.Layers)
            {
                ValidateAssetRef(document.Assets, layer.Asset, diagnostics, scene.SceneId, layer.LayerId);
            }

            foreach (var character in scene.Characters)
            {
                if (document.Characters.All(definition => definition.CharacterId != character.CharacterId))
                {
                    diagnostics.Add(new ProjectDiagnostic(
                        "POMPO003",
                        $"Scene '{scene.SceneId}' references missing character '{character.CharacterId}'.",
                        scene.SceneId,
                        character.PlacementId));
                }
            }
        }

        foreach (var character in document.Characters)
        {
            ValidateCharacterExpressions(character, diagnostics);
            foreach (var expression in character.Expressions)
            {
                ValidateAssetRef(document.Assets, expression.Sprite, diagnostics, character.CharacterId, expression.ExpressionId);
            }
        }

        ValidateGraphInventory(document, diagnostics);
        ValidateUniqueDocumentIds(document, diagnostics);
        ValidateLocales(document, diagnostics);
        ValidateRuntimeUiTheme(document, diagnostics);
        ValidateRuntimeUiSkin(document, diagnostics);
        ValidateRuntimeUiLayout(document, diagnostics);
        ValidateRuntimeUiAnimation(document, diagnostics);
        ValidateRuntimePlayback(document, diagnostics);
        ValidateStringTables(document, diagnostics);
        ValidateGraphReferences(document, diagnostics);
        ValidateGraphCallCycles(document, diagnostics);
        ValidateAssetDatabase(document, projectRoot, diagnostics);

        return new ProjectValidationResult(diagnostics);
    }

    private static void ValidateGraphInventory(
        PompoProjectDocument document,
        ICollection<ProjectDiagnostic> diagnostics)
    {
        if (document.Graphs.Count == 0)
        {
            diagnostics.Add(new ProjectDiagnostic(
                "POMPO026",
                "Project must define at least one graph."));
        }
    }

    private static void ValidateUniqueDocumentIds(
        PompoProjectDocument document,
        ICollection<ProjectDiagnostic> diagnostics)
    {
        ValidateUniqueIds(
            document.Scenes.Select(scene => scene.SceneId),
            "POMPO029",
            "Duplicate sceneId",
            diagnostics);
        ValidateUniqueIds(
            document.Characters.Select(character => character.CharacterId),
            "POMPO030",
            "Duplicate characterId",
            diagnostics);
        ValidateUniqueIds(
            document.Graphs.Select(graph => graph.GraphId),
            "POMPO031",
            "Duplicate graphId",
            diagnostics);

        foreach (var graph in document.Graphs)
        {
            if (!string.IsNullOrWhiteSpace(graph.GraphId) && !IsSafeFileId(graph.GraphId))
            {
                diagnostics.Add(new ProjectDiagnostic(
                    "POMPO037",
                    $"Graph '{graph.GraphId}' has an unsafe graphId. Use letters, numbers, '-', '_', or '.'.",
                    graph.GraphId,
                    graph.GraphId));
            }
        }
    }

    private static void ValidateUniqueIds(
        IEnumerable<string> ids,
        string code,
        string label,
        ICollection<ProjectDiagnostic> diagnostics)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (!seen.Add(id))
            {
                diagnostics.Add(new ProjectDiagnostic(code, $"{label} '{id}'.", id, id));
            }
        }
    }

    private static bool IsSafeFileId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) ||
            !string.Equals(id, id.Trim(), StringComparison.Ordinal) ||
            id is "." or ".." ||
            id.StartsWith(".", StringComparison.Ordinal) ||
            id.EndsWith(".", StringComparison.Ordinal))
        {
            return false;
        }

        return id.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');
    }

    private static void ValidateCharacterExpressions(
        CharacterDefinition character,
        ICollection<ProjectDiagnostic> diagnostics)
    {
        var expressionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var expression in character.Expressions)
        {
            if (string.IsNullOrWhiteSpace(expression.ExpressionId))
            {
                diagnostics.Add(new ProjectDiagnostic(
                    "POMPO034",
                    $"Character '{character.CharacterId}' has an empty expressionId.",
                    character.CharacterId));
                continue;
            }

            if (!expressionIds.Add(expression.ExpressionId))
            {
                diagnostics.Add(new ProjectDiagnostic(
                    "POMPO032",
                    $"Character '{character.CharacterId}' contains duplicate expression '{expression.ExpressionId}'.",
                    character.CharacterId,
                    expression.ExpressionId));
            }
        }

        if (!string.IsNullOrWhiteSpace(character.DefaultExpression) &&
            !expressionIds.Contains(character.DefaultExpression))
        {
            diagnostics.Add(new ProjectDiagnostic(
                "POMPO033",
                $"Character '{character.CharacterId}' default expression '{character.DefaultExpression}' does not exist.",
                character.CharacterId,
                character.DefaultExpression));
        }
    }

    private static void ValidateLocales(
        PompoProjectDocument document,
        ICollection<ProjectDiagnostic> diagnostics)
    {
        if (document.SupportedLocales.Count == 0)
        {
            diagnostics.Add(new ProjectDiagnostic("POMPO016", "Project must define at least one supported locale."));
            return;
        }

        var locales = new HashSet<string>(StringComparer.Ordinal);
        foreach (var locale in document.SupportedLocales)
        {
            if (string.IsNullOrWhiteSpace(locale))
            {
                diagnostics.Add(new ProjectDiagnostic("POMPO016", "Project contains an empty supported locale."));
            }
            else if (!locales.Add(locale))
            {
                diagnostics.Add(new ProjectDiagnostic("POMPO017", $"Duplicate supported locale '{locale}'.", null, locale));
            }
        }
    }

    private static void ValidateStringTables(
        PompoProjectDocument document,
        ICollection<ProjectDiagnostic> diagnostics)
    {
        var supportedLocales = new HashSet<string>(document.SupportedLocales.Where(locale => !string.IsNullOrWhiteSpace(locale)), StringComparer.Ordinal);
        var tableIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var table in document.StringTables)
        {
            if (string.IsNullOrWhiteSpace(table.TableId))
            {
                diagnostics.Add(new ProjectDiagnostic("POMPO018", "String table has an empty tableId."));
                continue;
            }

            if (!tableIds.Add(table.TableId))
            {
                diagnostics.Add(new ProjectDiagnostic("POMPO019", $"Duplicate string table '{table.TableId}'.", table.TableId));
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in table.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    diagnostics.Add(new ProjectDiagnostic("POMPO020", $"String table '{table.TableId}' contains an empty key.", table.TableId));
                    continue;
                }

                if (!keys.Add(entry.Key))
                {
                    diagnostics.Add(new ProjectDiagnostic("POMPO021", $"String table '{table.TableId}' contains duplicate key '{entry.Key}'.", table.TableId, entry.Key));
                }

                foreach (var locale in supportedLocales)
                {
                    if (!entry.Values.ContainsKey(locale))
                    {
                        diagnostics.Add(new ProjectDiagnostic("POMPO022", $"String '{table.TableId}:{entry.Key}' is missing locale '{locale}'.", table.TableId, entry.Key));
                    }
                }

                foreach (var locale in entry.Values.Keys)
                {
                    if (!supportedLocales.Contains(locale))
                    {
                        diagnostics.Add(new ProjectDiagnostic("POMPO023", $"String '{table.TableId}:{entry.Key}' contains unsupported locale '{locale}'.", table.TableId, entry.Key));
                    }
                }
            }
        }
    }

    private static void ValidateRuntimeUiTheme(
        PompoProjectDocument document,
        ICollection<ProjectDiagnostic> diagnostics)
    {
        var theme = document.RuntimeUiTheme ?? new PompoRuntimeUiTheme();
        foreach (var (name, value) in new (string Name, string Value)[]
        {
            (nameof(theme.CanvasClear), theme.CanvasClear),
            (nameof(theme.StageFallback), theme.StageFallback),
            (nameof(theme.StageActiveFallback), theme.StageActiveFallback),
            (nameof(theme.DialogueBackground), theme.DialogueBackground),
            (nameof(theme.NameBoxBackground), theme.NameBoxBackground),
            (nameof(theme.ChoiceBackground), theme.ChoiceBackground),
            (nameof(theme.ChoiceSelectedBackground), theme.ChoiceSelectedBackground),
            (nameof(theme.SaveMenuBackground), theme.SaveMenuBackground),
            (nameof(theme.SaveSlotBackground), theme.SaveSlotBackground),
            (nameof(theme.SaveSlotEmptyBackground), theme.SaveSlotEmptyBackground),
            (nameof(theme.BacklogBackground), theme.BacklogBackground),
            (nameof(theme.Text), theme.Text),
            (nameof(theme.MutedText), theme.MutedText),
            (nameof(theme.AccentText), theme.AccentText),
            (nameof(theme.HelpText), theme.HelpText)
        })
        {
            if (!IsValidThemeColor(value))
            {
                diagnostics.Add(new ProjectDiagnostic(
                    "POMPO038",
                    $"Runtime UI theme color '{name}' must use #RRGGBB or #RRGGBBAA.",
                    "runtimeUiTheme",
                    name));
            }
        }
    }

    private static bool IsValidThemeColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
            !value.StartsWith('#') ||
            value.Length is not 7 and not 9)
        {
            return false;
        }

        return value
            .Skip(1)
            .All(character =>
                char.IsDigit(character) ||
                character is >= 'a' and <= 'f' ||
                character is >= 'A' and <= 'F');
    }

    private static void ValidateRuntimeUiSkin(
        PompoProjectDocument document,
        ICollection<ProjectDiagnostic> diagnostics)
    {
        var skin = document.RuntimeUiSkin ?? new PompoRuntimeUiSkin();
        foreach (var (name, assetRef) in GetRuntimeUiSkinSlots(skin))
        {
            ValidateAssetRef(document.Assets, assetRef, diagnostics, "runtimeUiSkin", name);
        }
    }

    private static IEnumerable<(string Name, PompoAssetRef? AssetRef)> GetRuntimeUiSkinSlots(PompoRuntimeUiSkin skin)
    {
        yield return (nameof(skin.DialogueBox), skin.DialogueBox);
        yield return (nameof(skin.NameBox), skin.NameBox);
        yield return (nameof(skin.ChoiceBox), skin.ChoiceBox);
        yield return (nameof(skin.ChoiceSelectedBox), skin.ChoiceSelectedBox);
        yield return (nameof(skin.ChoiceDisabledBox), skin.ChoiceDisabledBox);
        yield return (nameof(skin.SaveMenuPanel), skin.SaveMenuPanel);
        yield return (nameof(skin.SaveSlot), skin.SaveSlot);
        yield return (nameof(skin.SaveSlotSelected), skin.SaveSlotSelected);
        yield return (nameof(skin.SaveSlotEmpty), skin.SaveSlotEmpty);
        yield return (nameof(skin.BacklogPanel), skin.BacklogPanel);
    }

    private static void ValidateRuntimeUiLayout(
        PompoProjectDocument document,
        ICollection<ProjectDiagnostic> diagnostics)
    {
        var layout = document.RuntimeUiLayout ?? new PompoRuntimeUiLayoutSettings();
        ValidateRuntimeUiRect(document, diagnostics, layout.DialogueTextBox, nameof(layout.DialogueTextBox));
        ValidateRuntimeUiRect(document, diagnostics, layout.DialogueNameBox, nameof(layout.DialogueNameBox));
        ValidateRuntimeUiRect(document, diagnostics, layout.SaveMenuBounds, nameof(layout.SaveMenuBounds));
        ValidateRuntimeUiRect(document, diagnostics, layout.BacklogBounds, nameof(layout.BacklogBounds));

        ValidatePositiveLayoutNumber(diagnostics, layout.ChoiceBoxWidth, nameof(layout.ChoiceBoxWidth));
        ValidatePositiveLayoutNumber(diagnostics, layout.ChoiceBoxHeight, nameof(layout.ChoiceBoxHeight));
        ValidateNonNegativeLayoutNumber(diagnostics, layout.ChoiceBoxSpacing, nameof(layout.ChoiceBoxSpacing));
        ValidatePositiveLayoutNumber(diagnostics, layout.SaveSlotHeight, nameof(layout.SaveSlotHeight));
        ValidateNonNegativeLayoutNumber(diagnostics, layout.SaveSlotSpacing, nameof(layout.SaveSlotSpacing));
    }

    private static void ValidateRuntimeUiAnimation(
        PompoProjectDocument document,
        ICollection<ProjectDiagnostic> diagnostics)
    {
        var animation = document.RuntimeUiAnimation ?? new PompoRuntimeUiAnimationSettings();
        if (animation.PanelFadeMilliseconds < 0)
        {
            diagnostics.Add(new ProjectDiagnostic(
                "POMPO040",
                "Runtime UI animation value 'PanelFadeMilliseconds' must be zero or greater.",
                "runtimeUiAnimation",
                nameof(animation.PanelFadeMilliseconds)));
        }

        if (animation.ChoicePulseMilliseconds < 0)
        {
            diagnostics.Add(new ProjectDiagnostic(
                "POMPO040",
                "Runtime UI animation value 'ChoicePulseMilliseconds' must be zero or greater.",
                "runtimeUiAnimation",
                nameof(animation.ChoicePulseMilliseconds)));
        }

        if (animation.TextRevealCharactersPerSecond < 0)
        {
            diagnostics.Add(new ProjectDiagnostic(
                "POMPO040",
                "Runtime UI animation value 'TextRevealCharactersPerSecond' must be zero or greater.",
                "runtimeUiAnimation",
                nameof(animation.TextRevealCharactersPerSecond)));
        }

        if (animation.ChoicePulseStrength is < 0f or > 1f ||
            float.IsNaN(animation.ChoicePulseStrength) ||
            float.IsInfinity(animation.ChoicePulseStrength))
        {
            diagnostics.Add(new ProjectDiagnostic(
                "POMPO041",
                "Runtime UI animation value 'ChoicePulseStrength' must be between 0 and 1.",
                "runtimeUiAnimation",
                nameof(animation.ChoicePulseStrength)));
        }
    }

    private static void ValidateRuntimePlayback(
        PompoProjectDocument document,
        ICollection<ProjectDiagnostic> diagnostics)
    {
        var playback = document.RuntimePlayback ?? new PompoRuntimePlaybackSettings();
        if (playback.AutoForwardDelayMilliseconds < 0)
        {
            diagnostics.Add(new ProjectDiagnostic(
                "POMPO042",
                "Runtime playback value 'AutoForwardDelayMilliseconds' must be zero or greater.",
                "runtimePlayback",
                nameof(playback.AutoForwardDelayMilliseconds)));
        }

        if (playback.SkipIntervalMilliseconds < 0)
        {
            diagnostics.Add(new ProjectDiagnostic(
                "POMPO042",
                "Runtime playback value 'SkipIntervalMilliseconds' must be zero or greater.",
                "runtimePlayback",
                nameof(playback.SkipIntervalMilliseconds)));
        }
    }

    private static void ValidateRuntimeUiRect(
        PompoProjectDocument document,
        ICollection<ProjectDiagnostic> diagnostics,
        PompoRuntimeUiRect? rect,
        string elementId)
    {
        if (rect is null ||
            rect.X < 0 ||
            rect.Y < 0 ||
            rect.Width <= 0 ||
            rect.Height <= 0 ||
            rect.X + rect.Width > document.VirtualWidth ||
            rect.Y + rect.Height > document.VirtualHeight)
        {
            diagnostics.Add(new ProjectDiagnostic(
                "POMPO039",
                $"Runtime UI layout rectangle '{elementId}' must fit inside the virtual canvas and use a positive size.",
                "runtimeUiLayout",
                elementId));
        }
    }

    private static void ValidatePositiveLayoutNumber(
        ICollection<ProjectDiagnostic> diagnostics,
        int value,
        string elementId)
    {
        if (value <= 0)
        {
            diagnostics.Add(new ProjectDiagnostic(
                "POMPO039",
                $"Runtime UI layout value '{elementId}' must be greater than zero.",
                "runtimeUiLayout",
                elementId));
        }
    }

    private static void ValidateNonNegativeLayoutNumber(
        ICollection<ProjectDiagnostic> diagnostics,
        int value,
        string elementId)
    {
        if (value < 0)
        {
            diagnostics.Add(new ProjectDiagnostic(
                "POMPO039",
                $"Runtime UI layout value '{elementId}' must be zero or greater.",
                "runtimeUiLayout",
                elementId));
        }
    }

    private static void ValidateGraphReferences(
        PompoProjectDocument document,
        ICollection<ProjectDiagnostic> diagnostics)
    {
        foreach (var graph in document.Graphs)
        {
            foreach (var node in graph.Nodes)
            {
                switch (node.Kind)
                {
                    case GraphNodeKind.Dialogue:
                        ValidateLocalizedTextRef(document, graph, node, "textKey", "tableId", diagnostics);
                        ValidateLocalizedTextRef(document, graph, node, "speakerKey", "speakerTableId", diagnostics);
                        break;

                    case GraphNodeKind.Narration:
                        ValidateLocalizedTextRef(document, graph, node, "textKey", "tableId", diagnostics);
                        break;

                    case GraphNodeKind.Choice:
                        ValidateChoiceLocalizedTextRefs(document, graph, node, diagnostics);
                        break;

                    case GraphNodeKind.ChangeBackground:
                        ValidateGraphAssetRef(document, graph, node, PompoAssetType.Image, "assetId", "backgroundAssetId", diagnostics);
                        break;

                    case GraphNodeKind.PlayBgm:
                        ValidateGraphAssetRef(document, graph, node, PompoAssetType.Audio, "assetId", "bgmAssetId", diagnostics);
                        break;

                    case GraphNodeKind.PlaySfx:
                        ValidateGraphAssetRef(document, graph, node, PompoAssetType.Audio, "assetId", "sfxAssetId", diagnostics);
                        break;

                    case GraphNodeKind.PlayVoice:
                        ValidateGraphAssetRef(document, graph, node, PompoAssetType.Audio, "assetId", "voiceAssetId", diagnostics);
                        break;

                    case GraphNodeKind.ShowCharacter:
                    case GraphNodeKind.HideCharacter:
                    case GraphNodeKind.MoveCharacter:
                    case GraphNodeKind.ChangeExpression:
                        ValidateGraphCharacterRef(document, graph, node, diagnostics);
                        break;

                    case GraphNodeKind.CallGraph:
                        ValidateCalledGraphRef(document, graph, node, diagnostics);
                        break;

                    case GraphNodeKind.Jump:
                        ValidateJumpTargetRef(graph, node, diagnostics);
                        break;
                }
            }
        }
    }

    private static void ValidateChoiceLocalizedTextRefs(
        PompoProjectDocument document,
        GraphDocument graph,
        GraphNode node,
        ICollection<ProjectDiagnostic> diagnostics)
    {
        ValidateLocalizedTextRef(document, graph, node, "textKey", "tableId", diagnostics);

        if (!node.Properties.TryGetPropertyValue("choices", out var choicesNode) ||
            choicesNode is not JsonArray choices)
        {
            return;
        }

        foreach (var choice in choices.OfType<JsonObject>())
        {
            ValidateChoiceStateProperties(graph, node, choice, diagnostics);

            var key = ReadString(choice, "textKey");
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var tableId = ReadString(choice, "tableId") ?? ReadString(node, "tableId") ?? StringTableLocalizer.DefaultTableId;
            ValidateStringTableEntry(document, tableId, key, graph.GraphId, node.NodeId, diagnostics);
        }
    }

    private static void ValidateChoiceStateProperties(
        GraphDocument graph,
        GraphNode node,
        JsonObject choice,
        ICollection<ProjectDiagnostic> diagnostics)
    {
        if (choice.TryGetPropertyValue("enabled", out var enabledNode) &&
            enabledNode?.GetValueKind() is not JsonValueKind.True and not JsonValueKind.False)
        {
            diagnostics.Add(new ProjectDiagnostic(
                "POMPO043",
                $"Graph '{graph.GraphId}' node '{node.NodeId}' has a choice with non-boolean 'enabled'.",
                graph.GraphId,
                node.NodeId));
        }

        if (!choice.TryGetPropertyValue("enabledVariable", out var enabledVariableNode) ||
            enabledVariableNode is null)
        {
            return;
        }

        if (enabledVariableNode.GetValueKind() != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(enabledVariableNode.GetValue<string>()))
        {
            diagnostics.Add(new ProjectDiagnostic(
                "POMPO043",
                $"Graph '{graph.GraphId}' node '{node.NodeId}' has a choice with invalid 'enabledVariable'.",
                graph.GraphId,
                node.NodeId));
        }
    }

    private static void ValidateLocalizedTextRef(
        PompoProjectDocument document,
        GraphDocument graph,
        GraphNode node,
        string keyProperty,
        string tableProperty,
        ICollection<ProjectDiagnostic> diagnostics)
    {
        var key = ReadString(node, keyProperty);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var tableId = ReadString(node, tableProperty) ?? StringTableLocalizer.DefaultTableId;
        ValidateStringTableEntry(document, tableId, key, graph.GraphId, node.NodeId, diagnostics);
    }

    private static void ValidateStringTableEntry(
        PompoProjectDocument document,
        string tableId,
        string key,
        string graphId,
        string nodeId,
        ICollection<ProjectDiagnostic> diagnostics)
    {
        var table = document.StringTables.FirstOrDefault(
            candidate => string.Equals(candidate.TableId, tableId, StringComparison.Ordinal));
        if (table is null)
        {
            diagnostics.Add(new ProjectDiagnostic(
                "POMPO024",
                $"Graph '{graphId}' node '{nodeId}' references missing string table '{tableId}'.",
                graphId,
                nodeId));
            return;
        }

        if (table.Entries.All(entry => !string.Equals(entry.Key, key, StringComparison.Ordinal)))
        {
            diagnostics.Add(new ProjectDiagnostic(
                "POMPO025",
                $"Graph '{graphId}' node '{nodeId}' references missing string key '{tableId}:{key}'.",
                graphId,
                nodeId));
        }
    }

    private static void ValidateGraphAssetRef(
        PompoProjectDocument document,
        GraphDocument graph,
        GraphNode node,
        PompoAssetType expectedType,
        string primaryProperty,
        string fallbackProperty,
        ICollection<ProjectDiagnostic> diagnostics)
    {
        var assetId = ReadString(node, primaryProperty) ?? ReadString(node, fallbackProperty);
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return;
        }

        var asset = document.Assets.Find(assetId);
        if (asset is null)
        {
            diagnostics.Add(new ProjectDiagnostic(
                "POMPO011",
                $"Graph '{graph.GraphId}' node '{node.NodeId}' references missing asset '{assetId}'.",
                graph.GraphId,
                node.NodeId));
            return;
        }

        if (asset.Type != expectedType)
        {
            diagnostics.Add(new ProjectDiagnostic(
                "POMPO012",
                $"Graph '{graph.GraphId}' node '{node.NodeId}' references asset '{assetId}' as '{asset.Type}', expected '{expectedType}'.",
                graph.GraphId,
                node.NodeId));
        }
    }

    private static void ValidateGraphCharacterRef(
        PompoProjectDocument document,
        GraphDocument graph,
        GraphNode node,
        ICollection<ProjectDiagnostic> diagnostics)
    {
        var characterId = ReadString(node, "characterId") ?? ReadString(node, "id");
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return;
        }

        var character = document.Characters.FirstOrDefault(
            definition => string.Equals(definition.CharacterId, characterId, StringComparison.Ordinal));
        if (character is null)
        {
            diagnostics.Add(new ProjectDiagnostic(
                "POMPO013",
                $"Graph '{graph.GraphId}' node '{node.NodeId}' references missing character '{characterId}'.",
                graph.GraphId,
                node.NodeId));
            return;
        }

        var expressionId = ReadString(node, "expressionId") ?? ReadString(node, "expression");
        if (!string.IsNullOrWhiteSpace(expressionId) &&
            character.Expressions.Count > 0 &&
            character.Expressions.All(expression => !string.Equals(expression.ExpressionId, expressionId, StringComparison.Ordinal)))
        {
            diagnostics.Add(new ProjectDiagnostic(
                "POMPO014",
                $"Graph '{graph.GraphId}' node '{node.NodeId}' references missing expression '{expressionId}' on character '{characterId}'.",
                graph.GraphId,
                node.NodeId));
        }
    }

    private static void ValidateCalledGraphRef(
        PompoProjectDocument document,
        GraphDocument graph,
        GraphNode node,
        ICollection<ProjectDiagnostic> diagnostics)
    {
        var graphId = ReadString(node, "graphId") ?? ReadString(node, "targetGraphId");
        if (string.IsNullOrWhiteSpace(graphId) ||
            document.Graphs.Any(candidate => string.Equals(candidate.GraphId, graphId, StringComparison.Ordinal)))
        {
            return;
        }

        diagnostics.Add(new ProjectDiagnostic(
            "POMPO015",
            $"Graph '{graph.GraphId}' node '{node.NodeId}' calls missing graph '{graphId}'.",
            graph.GraphId,
            node.NodeId));
    }

    private static void ValidateJumpTargetRef(
        GraphDocument graph,
        GraphNode node,
        ICollection<ProjectDiagnostic> diagnostics)
    {
        var targetNodeId = ReadString(node, "targetNodeId") ?? ReadString(node, "nodeId");
        if (string.IsNullOrWhiteSpace(targetNodeId) ||
            graph.Nodes.Any(candidate => string.Equals(candidate.NodeId, targetNodeId, StringComparison.Ordinal)))
        {
            return;
        }

        diagnostics.Add(new ProjectDiagnostic(
            "POMPO028",
            $"Graph '{graph.GraphId}' node '{node.NodeId}' jumps to missing node '{targetNodeId}'.",
            graph.GraphId,
            node.NodeId));
    }

    private static void ValidateGraphCallCycles(
        PompoProjectDocument document,
        ICollection<ProjectDiagnostic> diagnostics)
    {
        var graphsById = document.Graphs
            .Where(graph => !string.IsNullOrWhiteSpace(graph.GraphId))
            .GroupBy(graph => graph.GraphId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var outgoingCalls = graphsById.Values
            .ToDictionary(
                graph => graph.GraphId,
                graph => graph.Nodes
                    .Where(node => node.Kind == GraphNodeKind.CallGraph)
                    .Select(node => (NodeId: node.NodeId, TargetGraphId: ReadString(node, "graphId") ?? ReadString(node, "targetGraphId")))
                    .Where(call => !string.IsNullOrWhiteSpace(call.TargetGraphId) && graphsById.ContainsKey(call.TargetGraphId!))
                    .Select(call => (call.NodeId, TargetGraphId: call.TargetGraphId!))
                    .ToArray(),
                StringComparer.Ordinal);
        var state = graphsById.Keys.ToDictionary(graphId => graphId, _ => VisitState.Unvisited, StringComparer.Ordinal);
        var stack = new Stack<string>();
        var reported = new HashSet<string>(StringComparer.Ordinal);

        foreach (var graphId in graphsById.Keys)
        {
            VisitGraph(graphId);
        }

        void VisitGraph(string graphId)
        {
            if (state[graphId] == VisitState.Visited)
            {
                return;
            }

            if (state[graphId] == VisitState.Visiting)
            {
                return;
            }

            state[graphId] = VisitState.Visiting;
            stack.Push(graphId);

            foreach (var call in outgoingCalls[graphId])
            {
                if (state[call.TargetGraphId] == VisitState.Visiting)
                {
                    ReportCycle(graphId, call.NodeId, call.TargetGraphId);
                    continue;
                }

                VisitGraph(call.TargetGraphId);
            }

            stack.Pop();
            state[graphId] = VisitState.Visited;
        }

        void ReportCycle(string graphId, string nodeId, string targetGraphId)
        {
            var cycle = stack
                .Reverse()
                .SkipWhile(candidate => !string.Equals(candidate, targetGraphId, StringComparison.Ordinal))
                .Append(targetGraphId)
                .ToArray();
            var cycleKey = string.Join(">", cycle);
            if (!reported.Add(cycleKey))
            {
                return;
            }

            diagnostics.Add(new ProjectDiagnostic(
                "POMPO027",
                $"Graph call cycle detected: {string.Join(" -> ", cycle)}.",
                graphId,
                nodeId));
        }
    }

    private enum VisitState
    {
        Unvisited,
        Visiting,
        Visited
    }

    private static void ValidateAssetDatabase(
        PompoProjectDocument document,
        string? projectRoot,
        ICollection<ProjectDiagnostic> diagnostics)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var asset in document.Assets.Assets)
        {
            if (string.IsNullOrWhiteSpace(asset.AssetId))
            {
                diagnostics.Add(new ProjectDiagnostic("POMPO006", "Asset has an empty assetId.", asset.SourcePath));
            }
            else if (!AssetDatabaseService.IsValidAssetId(asset.AssetId))
            {
                diagnostics.Add(new ProjectDiagnostic(
                    "POMPO035",
                    $"Asset '{asset.AssetId}' has an unsafe assetId. Use letters, numbers, '-', '_', or '.'.",
                    asset.SourcePath,
                    asset.AssetId));
            }
            else if (!ids.Add(asset.AssetId))
            {
                diagnostics.Add(new ProjectDiagnostic("POMPO007", $"Duplicate assetId '{asset.AssetId}'.", asset.SourcePath, asset.AssetId));
            }

            if (string.IsNullOrWhiteSpace(asset.SourcePath))
            {
                diagnostics.Add(new ProjectDiagnostic("POMPO008", $"Asset '{asset.AssetId}' has an empty source path.", null, asset.AssetId));
            }
            else if (!AssetDatabaseService.IsSafeProjectRelativePath(asset.SourcePath))
            {
                diagnostics.Add(new ProjectDiagnostic(
                    "POMPO036",
                    $"Asset '{asset.AssetId}' has an unsafe source path '{asset.SourcePath}'.",
                    asset.SourcePath,
                    asset.AssetId));
            }

            if (string.IsNullOrWhiteSpace(asset.Hash))
            {
                diagnostics.Add(new ProjectDiagnostic("POMPO009", $"Asset '{asset.AssetId}' has an empty hash.", asset.SourcePath, asset.AssetId));
            }

            if (!string.IsNullOrWhiteSpace(projectRoot) &&
                !string.IsNullOrWhiteSpace(asset.SourcePath) &&
                !File.Exists(Path.Combine(projectRoot, asset.SourcePath)))
            {
                diagnostics.Add(new ProjectDiagnostic(
                    "POMPO010",
                    $"Asset file '{asset.SourcePath}' does not exist.",
                    asset.SourcePath,
                    asset.AssetId));
            }
        }
    }

    private static string? ReadString(GraphNode node, string propertyName)
    {
        return node.Properties.TryGetPropertyValue(propertyName, out var value)
            ? value?.GetValue<string>()
            : null;
    }

    private static string? ReadString(JsonObject node, string propertyName)
    {
        return node.TryGetPropertyValue(propertyName, out var value)
            ? value?.GetValue<string>()
            : null;
    }

    private static void ValidateAssetRef(
        AssetDatabase database,
        PompoAssetRef? assetRef,
        ICollection<ProjectDiagnostic> diagnostics,
        string documentPath,
        string? elementId)
    {
        if (assetRef is null)
        {
            return;
        }

        var asset = database.Find(assetRef.AssetId);
        if (asset is null)
        {
            diagnostics.Add(new ProjectDiagnostic(
                "POMPO004",
                $"Missing asset '{assetRef.AssetId}'.",
                documentPath,
                elementId));
            return;
        }

        if (asset.Type != assetRef.Type)
        {
            diagnostics.Add(new ProjectDiagnostic(
                "POMPO005",
                $"Asset '{assetRef.AssetId}' is '{asset.Type}', expected '{assetRef.Type}'.",
                documentPath,
                elementId));
        }
    }
}
