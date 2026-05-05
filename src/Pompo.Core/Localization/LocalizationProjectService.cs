using Pompo.Core.Project;

namespace Pompo.Core.Localization;

public sealed class LocalizationProjectService
{
    public void AddSupportedLocale(PompoProjectDocument project, string locale)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);

        var normalizedLocale = locale.Trim();
        if (!IsValidLocaleId(normalizedLocale))
        {
            throw new InvalidOperationException(
                $"Locale '{locale}' is invalid. Use letters, numbers, or '-'.");
        }

        if (project.SupportedLocales.Contains(normalizedLocale, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Locale '{normalizedLocale}' is already supported.");
        }

        project.SupportedLocales.Add(normalizedLocale);
    }

    public void DeleteSupportedLocale(PompoProjectDocument project, string locale)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);

        var normalizedLocale = locale.Trim();
        if (!project.SupportedLocales.Contains(normalizedLocale, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Locale '{normalizedLocale}' is not supported by the project.");
        }

        if (project.SupportedLocales.Count <= 1)
        {
            throw new InvalidOperationException("Project must keep at least one supported locale.");
        }

        project.SupportedLocales.RemoveAll(existing => string.Equals(existing, normalizedLocale, StringComparison.Ordinal));
        foreach (var table in project.StringTables)
        {
            foreach (var entry in table.Entries)
            {
                entry.Values.Remove(normalizedLocale);
            }
        }
    }

    private static bool IsValidLocaleId(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length > 0 &&
            !trimmed.StartsWith("-", StringComparison.Ordinal) &&
            !trimmed.EndsWith("-", StringComparison.Ordinal) &&
            trimmed.All(character => char.IsLetterOrDigit(character) || character == '-');
    }
}
