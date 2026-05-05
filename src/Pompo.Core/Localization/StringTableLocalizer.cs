namespace Pompo.Core.Localization;

public sealed class StringTableLocalizer
{
    public const string DefaultTableId = "ui";

    private readonly Dictionary<string, Dictionary<string, StringTableEntry>> _tables;

    public StringTableLocalizer(
        IEnumerable<StringTableDocument> tables,
        string locale,
        string? fallbackLocale = null)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);

        Locale = locale;
        FallbackLocale = fallbackLocale;
        _tables = tables
            .Where(table => !string.IsNullOrWhiteSpace(table.TableId))
            .GroupBy(table => table.TableId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .SelectMany(table => table.Entries)
                    .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
                    .GroupBy(entry => entry.Key, StringComparer.Ordinal)
                    .ToDictionary(entryGroup => entryGroup.Key, entryGroup => entryGroup.First(), StringComparer.Ordinal),
                StringComparer.Ordinal);
    }

    public string Locale { get; }

    public string? FallbackLocale { get; }

    public bool ContainsTable(string? tableId)
    {
        return _tables.ContainsKey(NormalizeTableId(tableId));
    }

    public bool ContainsKey(string? tableId, string key)
    {
        return _tables.TryGetValue(NormalizeTableId(tableId), out var entries) &&
            entries.ContainsKey(key);
    }

    public string Resolve(string? tableId, string key, string? fallback = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return fallback ?? string.Empty;
        }

        if (!_tables.TryGetValue(NormalizeTableId(tableId), out var entries) ||
            !entries.TryGetValue(key, out var entry))
        {
            return fallback ?? key;
        }

        if (TryReadLocaleValue(entry, Locale, out var localized))
        {
            return localized;
        }

        if (!string.IsNullOrWhiteSpace(FallbackLocale) &&
            TryReadLocaleValue(entry, FallbackLocale, out var fallbackLocalized))
        {
            return fallbackLocalized;
        }

        return fallback ?? key;
    }

    private static string NormalizeTableId(string? tableId)
    {
        return string.IsNullOrWhiteSpace(tableId) ? DefaultTableId : tableId;
    }

    private static bool TryReadLocaleValue(
        StringTableEntry entry,
        string locale,
        out string value)
    {
        if (entry.Values.TryGetValue(locale, out value!) &&
            !string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        value = string.Empty;
        return false;
    }
}
