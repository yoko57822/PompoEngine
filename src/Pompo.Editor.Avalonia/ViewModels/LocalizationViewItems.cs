namespace Pompo.Editor.Avalonia.ViewModels;

public sealed record LocalizationLocaleItem(
    string Locale,
    bool IsPreviewLocale);

public sealed record LocalizationStringEntryItem(
    string TableId,
    string Key,
    string ValuesSummary,
    bool HasMissingValues,
    bool HasUnsupportedValues);
