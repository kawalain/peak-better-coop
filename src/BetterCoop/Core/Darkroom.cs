namespace BetterCoop.Core;

/// <summary>
/// How a print comes up in the developer tray: from blank white paper, the
/// shadows first and the light tones last, as silver darkens where the most
/// light fell; warm at first, as a print still wet in the tray looks, and its
/// own colours once fixed.
/// </summary>
internal static class Darkroom
{
    /// <summary>How long a print takes to come up.</summary>
    public const float DevelopSeconds = 0.9f;

    /// <summary>The warm cast of a print still in the tray, multiplied over the picture.</summary>
    public static readonly Ink Wet = new(1f, 0.84f, 0.64f);

    /// <summary>
    /// How much blank paper still lies over the picture <paramref name="elapsed"/>
    /// seconds into developing, 1 to 0. Laid over the picture evenly, it lets
    /// the dark tones show through long before the light ones.
    /// </summary>
    public static float Blank(float elapsed)
    {
        float t = Clamp(elapsed / DevelopSeconds);
        // Slow to start, as nothing shows for a moment in the tray, then quick, then settling.
        return 1f - t * t * (3f - 2f * t);
    }

    /// <summary>How warm the picture still looks <paramref name="elapsed"/> seconds into developing, 1 to 0.</summary>
    public static float Warmth(float elapsed)
    {
        float t = Clamp((elapsed / DevelopSeconds - 0.35f) / 0.65f);
        return 1f - t * t * (3f - 2f * t);
    }

    private static float Clamp(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
}
