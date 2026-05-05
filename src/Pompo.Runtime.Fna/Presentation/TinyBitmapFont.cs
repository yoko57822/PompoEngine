using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Pompo.Runtime.Fna.Presentation;

public sealed class TinyBitmapFont
{
    private const int GlyphWidth = 5;
    private const int GlyphHeight = 7;
    private static readonly IReadOnlyDictionary<char, string[]> Glyphs = CreateGlyphs();

    public int MeasureWidth(string text, int scale)
    {
        return string.IsNullOrEmpty(text) ? 0 : text.Length * ((GlyphWidth + 1) * scale);
    }

    public int LineHeight(int scale)
    {
        return (GlyphHeight + 2) * scale;
    }

    public void DrawText(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        string text,
        int x,
        int y,
        int scale,
        Color color)
    {
        var cursorX = x;
        foreach (var character in text)
        {
            DrawGlyph(spriteBatch, pixel, char.ToUpperInvariant(character), cursorX, y, scale, color);
            cursorX += (GlyphWidth + 1) * scale;
        }
    }

    private static void DrawGlyph(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        char character,
        int x,
        int y,
        int scale,
        Color color)
    {
        if (character == ' ')
        {
            return;
        }

        if (!Glyphs.TryGetValue(character, out var glyph))
        {
            glyph = Glyphs['?'];
        }

        for (var row = 0; row < GlyphHeight; row++)
        {
            for (var column = 0; column < GlyphWidth; column++)
            {
                if (glyph[row][column] != '#')
                {
                    continue;
                }

                spriteBatch.Draw(
                    pixel,
                    new Rectangle(x + (column * scale), y + (row * scale), scale, scale),
                    color);
            }
        }
    }

    private static IReadOnlyDictionary<char, string[]> CreateGlyphs()
    {
        return new Dictionary<char, string[]>
        {
            ['A'] = [" ### ", "#   #", "#   #", "#####", "#   #", "#   #", "#   #"],
            ['B'] = ["#### ", "#   #", "#   #", "#### ", "#   #", "#   #", "#### "],
            ['C'] = [" ####", "#    ", "#    ", "#    ", "#    ", "#    ", " ####"],
            ['D'] = ["#### ", "#   #", "#   #", "#   #", "#   #", "#   #", "#### "],
            ['E'] = ["#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#####"],
            ['F'] = ["#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#    "],
            ['G'] = [" ####", "#    ", "#    ", "#  ##", "#   #", "#   #", " ####"],
            ['H'] = ["#   #", "#   #", "#   #", "#####", "#   #", "#   #", "#   #"],
            ['I'] = ["#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "#####"],
            ['J'] = ["#####", "   # ", "   # ", "   # ", "#  # ", "#  # ", " ##  "],
            ['K'] = ["#   #", "#  # ", "# #  ", "##   ", "# #  ", "#  # ", "#   #"],
            ['L'] = ["#    ", "#    ", "#    ", "#    ", "#    ", "#    ", "#####"],
            ['M'] = ["#   #", "## ##", "# # #", "#   #", "#   #", "#   #", "#   #"],
            ['N'] = ["#   #", "##  #", "# # #", "#  ##", "#   #", "#   #", "#   #"],
            ['O'] = [" ### ", "#   #", "#   #", "#   #", "#   #", "#   #", " ### "],
            ['P'] = ["#### ", "#   #", "#   #", "#### ", "#    ", "#    ", "#    "],
            ['Q'] = [" ### ", "#   #", "#   #", "#   #", "# # #", "#  # ", " ## #"],
            ['R'] = ["#### ", "#   #", "#   #", "#### ", "# #  ", "#  # ", "#   #"],
            ['S'] = [" ####", "#    ", "#    ", " ### ", "    #", "    #", "#### "],
            ['T'] = ["#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  "],
            ['U'] = ["#   #", "#   #", "#   #", "#   #", "#   #", "#   #", " ### "],
            ['V'] = ["#   #", "#   #", "#   #", "#   #", "#   #", " # # ", "  #  "],
            ['W'] = ["#   #", "#   #", "#   #", "# # #", "# # #", "## ##", "#   #"],
            ['X'] = ["#   #", "#   #", " # # ", "  #  ", " # # ", "#   #", "#   #"],
            ['Y'] = ["#   #", "#   #", " # # ", "  #  ", "  #  ", "  #  ", "  #  "],
            ['Z'] = ["#####", "    #", "   # ", "  #  ", " #   ", "#    ", "#####"],
            ['0'] = [" ### ", "#   #", "#  ##", "# # #", "##  #", "#   #", " ### "],
            ['1'] = ["  #  ", " ##  ", "# #  ", "  #  ", "  #  ", "  #  ", "#####"],
            ['2'] = [" ### ", "#   #", "    #", "   # ", "  #  ", " #   ", "#####"],
            ['3'] = ["#### ", "    #", "    #", " ### ", "    #", "    #", "#### "],
            ['4'] = ["#   #", "#   #", "#   #", "#####", "    #", "    #", "    #"],
            ['5'] = ["#####", "#    ", "#    ", "#### ", "    #", "    #", "#### "],
            ['6'] = [" ### ", "#    ", "#    ", "#### ", "#   #", "#   #", " ### "],
            ['7'] = ["#####", "    #", "   # ", "  #  ", " #   ", " #   ", " #   "],
            ['8'] = [" ### ", "#   #", "#   #", " ### ", "#   #", "#   #", " ### "],
            ['9'] = [" ### ", "#   #", "#   #", " ####", "    #", "    #", " ### "],
            ['.'] = ["     ", "     ", "     ", "     ", "     ", " ##  ", " ##  "],
            [','] = ["     ", "     ", "     ", "     ", " ##  ", " ##  ", " #   "],
            ['!'] = ["  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "     ", "  #  "],
            ['?'] = [" ### ", "#   #", "    #", "   # ", "  #  ", "     ", "  #  "],
            [':'] = ["     ", " ##  ", " ##  ", "     ", " ##  ", " ##  ", "     "],
            [';'] = ["     ", " ##  ", " ##  ", "     ", " ##  ", " ##  ", " #   "],
            ['-'] = ["     ", "     ", "     ", " ### ", "     ", "     ", "     "],
            ['_'] = ["     ", "     ", "     ", "     ", "     ", "     ", "#####"],
            ['\''] = [" ##  ", " ##  ", " #   ", "     ", "     ", "     ", "     "],
            ['"'] = ["# #  ", "# #  ", "     ", "     ", "     ", "     ", "     "],
            ['/'] = ["    #", "   # ", "   # ", "  #  ", " #   ", " #   ", "#    "],
            ['('] = ["   # ", "  #  ", " #   ", " #   ", " #   ", "  #  ", "   # "],
            [')'] = [" #   ", "  #  ", "   # ", "   # ", "   # ", "  #  ", " #   "],
            ['+'] = ["     ", "  #  ", "  #  ", "#####", "  #  ", "  #  ", "     "],
            ['='] = ["     ", "     ", "#####", "     ", "#####", "     ", "     "]
        };
    }
}
