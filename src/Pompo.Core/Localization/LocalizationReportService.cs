namespace Pompo.Core.Localization;

public sealed record LocalizationReport(
    int SupportedLocaleCount,
    int StringTableCount,
    int EntryCount,
    int MissingValueCount,
    int UnsupportedValueCount);

public sealed class LocalizationReportService
{
    public LocalizationReport Create(
        IReadOnlyList<StringTableDocument> stringTables,
        IReadOnlyList<string> supportedLocales)
    {
        ArgumentNullException.ThrowIfNull(stringTables);
        ArgumentNullException.ThrowIfNull(supportedLocales);

        var locales = supportedLocales
            .Where(locale => !string.IsNullOrWhiteSpace(locale))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var localeLookup = locales.ToHashSet(StringComparer.Ordinal);
        var entryCount = 0;
        var missing = 0;
        var unsupported = 0;

        foreach (var table in stringTables)
        {
            foreach (var entry in table.Entries)
            {
                entryCount++;
                missing += locales.Count(locale => !entry.Values.ContainsKey(locale));
                unsupported += entry.Values.Keys.Count(locale => !localeLookup.Contains(locale));
            }
        }

        return new LocalizationReport(
            locales.Length,
            stringTables.Count,
            entryCount,
            missing,
            unsupported);
    }
}
