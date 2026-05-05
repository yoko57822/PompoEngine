using System.Text.Json;
using System.Text.Json.Nodes;
using Pompo.Core.Project;

namespace Pompo.Core.Runtime;

public sealed class RuntimeSaveStore
{
    private static readonly JsonSerializerOptions JsonOptions = ProjectFileService.CreateJsonOptions();

    public async Task SaveAsync(
        string saveRoot,
        string slotId,
        string displayName,
        RuntimeSaveData data,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saveRoot);
        ValidateSlotId(slotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Directory.CreateDirectory(saveRoot);
        var metadata = new RuntimeSaveSlotMetadata(
            slotId,
            displayName,
            DateTimeOffset.UtcNow,
            data.GraphId,
            data.NodeId);
        var saveFile = new RuntimeSaveFile(metadata, data);
        var targetPath = GetSlotPath(saveRoot, slotId);
        var tempPath = $"{targetPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, saveFile, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(tempPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public async Task<RuntimeSaveFile> LoadAsync(
        string saveRoot,
        string slotId,
        CancellationToken cancellationToken = default)
    {
        ValidateSlotId(slotId);
        var path = GetSlotPath(saveRoot, slotId);
        await using var stream = File.OpenRead(path);
        var saveFile = await JsonSerializer.DeserializeAsync<RuntimeSaveFile>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        if (saveFile is null)
        {
            throw new InvalidDataException($"Save slot '{slotId}' is empty or invalid.");
        }

        return Normalize(saveFile);
    }

    public async Task<IReadOnlyList<RuntimeSaveSlotMetadata>> ListAsync(
        string saveRoot,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(saveRoot))
        {
            return [];
        }

        var slots = new List<RuntimeSaveSlotMetadata>();
        foreach (var path in Directory.EnumerateFiles(saveRoot, "*.pompo-save.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                await using var stream = File.OpenRead(path);
                var saveFile = await JsonSerializer.DeserializeAsync<RuntimeSaveFile>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                if (saveFile is not null && IsValidSlotId(saveFile.Metadata.SlotId))
                {
                    slots.Add(Normalize(saveFile).Metadata);
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidDataException)
            {
                continue;
            }
        }

        return slots
            .OrderByDescending(slot => slot.SavedAt)
            .ThenBy(slot => slot.SlotId, StringComparer.Ordinal)
            .ToArray();
    }

    public Task DeleteAsync(string saveRoot, string slotId)
    {
        ValidateSlotId(slotId);
        var path = GetSlotPath(saveRoot, slotId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public static string GetSlotPath(string saveRoot, string slotId)
    {
        ValidateSlotId(slotId);
        return Path.Combine(saveRoot, $"{slotId}.pompo-save.json");
    }

    private static void ValidateSlotId(string slotId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        if (!IsValidSlotId(slotId))
        {
            throw new ArgumentException("Slot id can contain only letters, digits, '-' and '_'.", nameof(slotId));
        }
    }

    private static bool IsValidSlotId(string slotId)
    {
        return !string.IsNullOrWhiteSpace(slotId) &&
            string.Equals(slotId, slotId.Trim(), StringComparison.Ordinal) &&
            slotId.All(character => char.IsLetterOrDigit(character) || character is '-' or '_');
    }

    private static RuntimeSaveFile Normalize(RuntimeSaveFile saveFile)
    {
        return saveFile with
        {
            Data = saveFile.Data with
            {
                Variables = saveFile.Data.Variables.ToDictionary(
                    variable => variable.Key,
                    variable => NormalizeValue(variable.Value),
                    StringComparer.Ordinal)
            }
        };
    }

    private static object? NormalizeValue(object? value)
    {
        if (value is long longValue &&
            longValue >= int.MinValue &&
            longValue <= int.MaxValue)
        {
            return (int)longValue;
        }

        if (value is double doubleValue &&
            Math.Abs(doubleValue % 1) < double.Epsilon &&
            doubleValue >= int.MinValue &&
            doubleValue <= int.MaxValue)
        {
            return (int)doubleValue;
        }

        if (value is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<bool>(out var boolValue))
            {
                return boolValue;
            }

            if (jsonValue.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }

            if (jsonValue.TryGetValue<double>(out var jsonDoubleValue))
            {
                return NormalizeValue(jsonDoubleValue);
            }

            if (jsonValue.TryGetValue<string>(out var stringValue))
            {
                return stringValue;
            }
        }

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number when element.TryGetInt32(out var intValue) => intValue,
                JsonValueKind.Number when element.TryGetDouble(out var elementDoubleValue) => NormalizeValue(elementDoubleValue),
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Null => null,
                _ => JsonNode.Parse(element.GetRawText())
            };
        }

        return value;
    }
}
