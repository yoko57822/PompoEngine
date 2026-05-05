using Pompo.Core.Project;

namespace Pompo.Runtime.Fna.Presentation;

public sealed class RuntimeTextReveal
{
    private readonly PompoRuntimeUiAnimationSettings _settings;
    private string? _text;
    private double _carry;

    public RuntimeTextReveal(PompoRuntimeUiAnimationSettings? settings = null)
    {
        _settings = settings ?? new PompoRuntimeUiAnimationSettings();
        VisibleCharacters = int.MaxValue;
    }

    public int VisibleCharacters { get; private set; }

    public bool IsComplete(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        return VisibleCharacters >= text.Length;
    }

    public string GetVisibleText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        SyncText(text);
        if (IsInstant)
        {
            return text;
        }

        return text[..Math.Clamp(VisibleCharacters, 0, text.Length)];
    }

    public void Update(string? text, double elapsedSeconds)
    {
        if (string.IsNullOrEmpty(text))
        {
            Reset(null, int.MaxValue);
            return;
        }

        SyncText(text);
        if (IsInstant || IsComplete(text))
        {
            VisibleCharacters = text.Length;
            return;
        }

        _carry += Math.Max(0d, elapsedSeconds) * _settings.TextRevealCharactersPerSecond;
        var charactersToReveal = (int)Math.Floor(_carry);
        if (charactersToReveal <= 0)
        {
            return;
        }

        _carry -= charactersToReveal;
        VisibleCharacters = Math.Min(text.Length, VisibleCharacters + charactersToReveal);
    }

    public bool Complete(string? text)
    {
        if (string.IsNullOrEmpty(text) || IsComplete(text))
        {
            return false;
        }

        Reset(text, text.Length);
        return true;
    }

    private bool IsInstant => !_settings.Enabled || _settings.TextRevealCharactersPerSecond <= 0;

    private void SyncText(string text)
    {
        if (string.Equals(_text, text, StringComparison.Ordinal))
        {
            return;
        }

        Reset(text, IsInstant ? text.Length : 0);
    }

    private void Reset(string? text, int visibleCharacters)
    {
        _text = text;
        _carry = 0d;
        VisibleCharacters = visibleCharacters;
    }
}
