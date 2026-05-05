using Microsoft.Xna.Framework.Audio;
using Pompo.Core.Runtime;

namespace Pompo.Runtime.Fna.Presentation;

public sealed class RuntimeAudioPlayer : IDisposable
{
    private readonly List<SoundEffect> _loadedEffects = [];
    private readonly List<SoundEffectInstance> _instances = [];
    private bool _disposed;

    public void Apply(RuntimeAudioState audio, RuntimeAssetCatalog? assetCatalog)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        StopAll();
        if (assetCatalog is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(audio.BgmAssetId))
        {
            Play(assetCatalog, audio.BgmAssetId, loop: true);
        }

        foreach (var sfx in audio.PlayingSfxAssetIds)
        {
            Play(assetCatalog, sfx, loop: false);
        }

        if (!string.IsNullOrWhiteSpace(audio.VoiceAssetId))
        {
            Play(assetCatalog, audio.VoiceAssetId, loop: false);
        }
    }

    private void Play(RuntimeAssetCatalog assetCatalog, string assetId, bool loop)
    {
        var path = assetCatalog.ResolveAssetPath(assetId);
        if (path is null)
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var effect = SoundEffect.FromStream(stream);
            var instance = effect.CreateInstance();
            instance.IsLooped = loop;
            instance.Play();
            _loadedEffects.Add(effect);
            _instances.Add(instance);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException)
        {
            Console.Error.WriteLine($"Could not play runtime audio '{path}': {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopAll();
        _disposed = true;
    }

    public void StopAll()
    {
        foreach (var instance in _instances)
        {
            instance.Dispose();
        }

        foreach (var effect in _loadedEffects)
        {
            effect.Dispose();
        }

        _instances.Clear();
        _loadedEffects.Clear();
    }
}
