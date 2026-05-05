namespace Pompo.Core.Localization;

public sealed record StringTableDocument(
    string TableId,
    List<StringTableEntry> Entries);

public sealed record StringTableEntry(
    string Key,
    Dictionary<string, string> Values);
