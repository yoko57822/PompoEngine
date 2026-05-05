using Microsoft.Xna.Framework;
using Pompo.Core.Project;

namespace Pompo.Runtime.Fna.Presentation;

public sealed record RuntimeUiThemeColors(
    Color CanvasClear,
    Color StageFallback,
    Color StageActiveFallback,
    Color DialogueBackground,
    Color NameBoxBackground,
    Color ChoiceBackground,
    Color ChoiceSelectedBackground,
    Color SaveMenuBackground,
    Color SaveSlotBackground,
    Color SaveSlotEmptyBackground,
    Color BacklogBackground,
    Color Text,
    Color MutedText,
    Color AccentText,
    Color HelpText)
{
    public static RuntimeUiThemeColors Default { get; } = FromProjectTheme(new PompoRuntimeUiTheme());

    public static RuntimeUiThemeColors FromProjectTheme(PompoRuntimeUiTheme? theme)
    {
        theme ??= new PompoRuntimeUiTheme();
        var defaults = new PompoRuntimeUiTheme();
        return new RuntimeUiThemeColors(
            Parse(theme.CanvasClear, defaults.CanvasClear),
            Parse(theme.StageFallback, defaults.StageFallback),
            Parse(theme.StageActiveFallback, defaults.StageActiveFallback),
            Parse(theme.DialogueBackground, defaults.DialogueBackground),
            Parse(theme.NameBoxBackground, defaults.NameBoxBackground),
            Parse(theme.ChoiceBackground, defaults.ChoiceBackground),
            Parse(theme.ChoiceSelectedBackground, defaults.ChoiceSelectedBackground),
            Parse(theme.SaveMenuBackground, defaults.SaveMenuBackground),
            Parse(theme.SaveSlotBackground, defaults.SaveSlotBackground),
            Parse(theme.SaveSlotEmptyBackground, defaults.SaveSlotEmptyBackground),
            Parse(theme.BacklogBackground, defaults.BacklogBackground),
            Parse(theme.Text, defaults.Text),
            Parse(theme.MutedText, defaults.MutedText),
            Parse(theme.AccentText, defaults.AccentText),
            Parse(theme.HelpText, defaults.HelpText));
    }

    private static Color Parse(string? value, string fallback)
    {
        var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (text.StartsWith('#'))
        {
            text = text[1..];
        }

        if (text.Length is not 6 and not 8 ||
            !uint.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var rgba))
        {
            return Parse(fallback, "#FFFFFF");
        }

        var r = (byte)((rgba >> (text.Length == 8 ? 24 : 16)) & 0xff);
        var g = (byte)((rgba >> (text.Length == 8 ? 16 : 8)) & 0xff);
        var b = (byte)((rgba >> (text.Length == 8 ? 8 : 0)) & 0xff);
        var a = text.Length == 8 ? (byte)(rgba & 0xff) : byte.MaxValue;
        return new Color(r, g, b, a);
    }
}
