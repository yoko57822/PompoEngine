using Pompo.Core.Project;

namespace Pompo.Editor.Avalonia.ViewModels;

public sealed record RuntimeAnimationPreset(
    string PresetId,
    string DisplayName,
    string Description,
    PompoRuntimeUiAnimationSettings Animation,
    PompoRuntimePlaybackSettings Playback);
