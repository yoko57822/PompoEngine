namespace Pompo.Core.Runtime;

public enum RuntimeLayer
{
    Background,
    CharacterBack,
    Character,
    CharacterFront,
    Effects,
    DialogueUI,
    Overlay
}

public sealed record RuntimeCharacterState(
    string CharacterId,
    string ExpressionId,
    RuntimeLayer Layer,
    float X,
    float Y,
    bool Visible);

public sealed record RuntimeAudioState(string? BgmAssetId, IReadOnlyList<string> PlayingSfxAssetIds)
{
    public string? VoiceAssetId { get; init; }
}

public sealed record RuntimeSaveData(
    int SchemaVersion,
    string GraphId,
    string NodeId,
    IReadOnlyList<string> CallStack,
    IReadOnlyDictionary<string, object?> Variables,
    string? BackgroundAssetId,
    IReadOnlyList<RuntimeCharacterState> Characters,
    RuntimeAudioState Audio,
    IReadOnlyList<string> ChoiceHistory)
{
    public IReadOnlyList<string> UnlockedCgIds { get; init; } = [];
}

public sealed record RuntimeSaveSlotMetadata(
    string SlotId,
    string DisplayName,
    DateTimeOffset SavedAt,
    string GraphId,
    string NodeId);

public sealed record RuntimeSaveFile(RuntimeSaveSlotMetadata Metadata, RuntimeSaveData Data);
