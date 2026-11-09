namespace BetterCoop.Core;

/// <summary>
/// A copy coming off a spirit duplicator's drum: the sheet is printed from
/// its top edge down as the drum turns over it, so the words come up line by
/// line, each first in the master's violet, still wet with spirit, and
/// settling into its own ink just behind the drum.
/// </summary>
internal static class Ditto
{
    /// <summary>How long the drum takes to turn the whole sheet.</summary>
    public const float TurnSeconds = 0.8f;

    /// <summary>The master's aniline violet.</summary>
    public static readonly Ink Violet = new(0.42f, 0.26f, 0.62f);

    /// <summary>How deep, as a share of the sheet, the drum's soft edge reaches.</summary>
    private const float Edge = 0.18f;

    /// <summary>How far behind the drum, as a share of the sheet, the words lose their violet.</summary>
    private const float Wet = 0.3f;

    /// <summary>
    /// How a word <paramref name="depth"/> down the sheet (0 at its top, 1 at
    /// its foot) shows <paramref name="elapsed"/> seconds after the drum
    /// started: how much of it is printed, and how much of the master's
    /// violet it still has, each 0 to 1.
    /// </summary>
    public static (float Printed, float Violet) At(float depth, float elapsed)
    {
        // The drum's line runs from just above the sheet to far enough below it that its foot has dried.
        float drum = -Edge + (1f + 2f * Edge + Wet) * Clamp(elapsed / TurnSeconds);
        float printed = Smooth((drum - depth) / Edge);
        float violet = 1f - Smooth((drum - depth - Edge) / Wet);
        return (printed, printed * violet);
    }

    /// <summary>Whether the drum has passed the whole sheet and the words have settled.</summary>
    public static bool Done(float elapsed) => elapsed >= TurnSeconds;

    private static float Smooth(float t)
    {
        t = Clamp(t);
        return t * t * (3f - 2f * t);
    }

    private static float Clamp(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
}
