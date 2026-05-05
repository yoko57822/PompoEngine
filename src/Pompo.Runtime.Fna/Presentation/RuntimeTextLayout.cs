namespace Pompo.Runtime.Fna.Presentation;

public static class RuntimeTextLayout
{
    public static IReadOnlyList<string> Wrap(string text, int maxCharactersPerLine, int maxLines)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCharactersPerLine);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLines);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            AppendWrappedParagraph(paragraph, maxCharactersPerLine, maxLines, lines);
            if (lines.Count >= maxLines)
            {
                break;
            }
        }

        return lines;
    }

    private static void AppendWrappedParagraph(
        string paragraph,
        int maxCharactersPerLine,
        int maxLines,
        ICollection<string> lines)
    {
        var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return;
        }

        var current = string.Empty;
        foreach (var word in words)
        {
            if (lines.Count >= maxLines)
            {
                return;
            }

            if (word.Length > maxCharactersPerLine)
            {
                if (!string.IsNullOrEmpty(current))
                {
                    lines.Add(current);
                    current = string.Empty;
                }

                for (var offset = 0; offset < word.Length && lines.Count < maxLines; offset += maxCharactersPerLine)
                {
                    lines.Add(word[offset..Math.Min(word.Length, offset + maxCharactersPerLine)]);
                }

                continue;
            }

            var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            if (candidate.Length <= maxCharactersPerLine)
            {
                current = candidate;
                continue;
            }

            lines.Add(current);
            current = word;
        }

        if (!string.IsNullOrEmpty(current) && lines.Count < maxLines)
        {
            lines.Add(current);
        }
    }
}
