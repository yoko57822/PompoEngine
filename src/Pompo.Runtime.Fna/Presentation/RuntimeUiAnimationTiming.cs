using Pompo.Core.Project;

namespace Pompo.Runtime.Fna.Presentation;

public sealed record RuntimeUiAnimationValues(
    float PanelOpacity,
    float SelectedChoiceScale);

public static class RuntimeUiAnimationTiming
{
    public static RuntimeUiAnimationValues Evaluate(
        PompoRuntimeUiAnimationSettings? settings,
        double elapsedSeconds)
    {
        settings ??= new PompoRuntimeUiAnimationSettings();
        if (!settings.Enabled)
        {
            return new RuntimeUiAnimationValues(1f, 1f);
        }

        var panelOpacity = EvaluatePanelOpacity(settings.PanelFadeMilliseconds, elapsedSeconds);
        var choiceScale = EvaluateChoiceScale(
            settings.ChoicePulseMilliseconds,
            settings.ChoicePulseStrength,
            elapsedSeconds);
        return new RuntimeUiAnimationValues(panelOpacity, choiceScale);
    }

    private static float EvaluatePanelOpacity(
        int fadeMilliseconds,
        double elapsedSeconds)
    {
        if (fadeMilliseconds <= 0)
        {
            return 1f;
        }

        var elapsedMilliseconds = Math.Max(0, elapsedSeconds * 1000d);
        return (float)Math.Clamp(elapsedMilliseconds / fadeMilliseconds, 0d, 1d);
    }

    private static float EvaluateChoiceScale(
        int pulseMilliseconds,
        float pulseStrength,
        double elapsedSeconds)
    {
        if (pulseMilliseconds <= 0 || pulseStrength <= 0f)
        {
            return 1f;
        }

        var cycle = (elapsedSeconds * 1000d / pulseMilliseconds) % 1d;
        var wave = (Math.Sin(cycle * Math.PI * 2d) + 1d) / 2d;
        return 1f + ((float)wave * pulseStrength);
    }
}
