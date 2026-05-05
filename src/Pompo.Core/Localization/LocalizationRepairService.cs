namespace Pompo.Core.Localization;

public sealed record LocalizationRepairResult(int FilledValueCount);

public sealed class LocalizationRepairService
{
    public LocalizationRepairResult FillMissingValues(
        IReadOnlyList<StringTableDocument> stringTables,
        IReadOnlyList<string> supportedLocales,
        string? preferredFallbackLocale = null)
    {
        ArgumentNullException.ThrowIfNull(stringTables);
        ArgumentNullException.ThrowIfNull(supportedLocales);

        var locales = supportedLocales
            .Where(locale => !string.IsNullOrWhiteSpace(locale))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var filled = 0;
        foreach (var table in stringTables)
        {
            foreach (var entry in table.Entries)
            {
                foreach (var locale in locales)
                {
                    if (!entry.Values.ContainsKey(locale))
                    {
                        entry.Values[locale] = FindFallbackValue(entry.Values, locales, preferredFallbackLocale) ?? entry.Key;
                        filled++;
                    }
                }
            }
        }

        return new LocalizationRepairResult(filled);
    }

    private static string? FindFallbackValue(
        IReadOnlyDictionary<string, string> values,
        IReadOnlyList<string> supportedLocales,
        string? preferredFallbackLocale)
    {
        if (!string.IsNullOrWhiteSpace(preferredFallbackLocale) &&
            values.TryGetValue(preferredFallbackLocale, out var preferred) &&
            !string.IsNullOrWhiteSpace(preferred))
        {
            return preferred;
        }

        foreach (var locale in supportedLocales)
        {
            if (values.TryGetValue(locale, out var supported) &&
                !string.IsNullOrWhiteSpace(supported))
            {
                return supported;
            }
        }

        return values.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
