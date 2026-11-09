namespace BetterCoop.Core;

/// <summary>
/// How a typewriter puts letters on paper: each type bar lands a little off
/// the line and leaves its own weight of ribbon ink, the same for the same
/// letter in the same place every time; and new words go on a letter at a
/// time, quickly, never taking long however many there are.
/// </summary>
internal static class Typewriter
{
    /// <summary>How long a letter takes to type.</summary>
    public const float LetterSeconds = 0.035f;

    /// <summary>The longest a line takes to type, however long it is.</summary>
    public const float LineSeconds = 0.4f;

    /// <summary>How far a letter may land off the line, as a share of the type's size.</summary>
    public const float Drift = 0.035f;

    /// <summary>How far a letter may lean, in degrees.</summary>
    public const float Lean = 1.6f;

    /// <summary>The lightest a worn ribbon leaves a letter, as a share of full ink.</summary>
    public const float Faintest = 0.74f;

    /// <summary>
    /// Where the letter <paramref name="letter"/> at <paramref name="index"/>
    /// lands: how far up (<c>Rise</c>, a share of the type's size), how far
    /// along (<c>Shift</c>, likewise), how it leans (<c>Lean</c>, degrees),
    /// and how much ink it takes (<c>Ink</c>, <see cref="Faintest"/> to 1).
    /// </summary>
    public static (float Rise, float Shift, float Lean, float Ink) Strike(int index, char letter, int seed = 0)
    {
        unchecked
        {
            uint h = (uint)(index * 73856093) ^ (uint)(letter * 19349663) ^ (uint)(seed * 83492791);
            float a = Unit(ref h);
            float b = Unit(ref h);
            float c = Unit(ref h);
            float d = Unit(ref h);
            // Most letters strike fairly evenly; a few come out light.
            float ink = 1f - (1f - Faintest) * d * d * d;
            return ((a - 0.5f) * 2f * Drift, (b - 0.5f) * Drift, (c - 0.5f) * 2f * Lean, ink);
        }
    }

    /// <summary>
    /// How many of <paramref name="length"/> letters are typed
    /// <paramref name="elapsed"/> seconds after typing began: one per
    /// <see cref="LetterSeconds"/>, faster for a line too long to type in
    /// <see cref="LineSeconds"/>.
    /// </summary>
    public static int Typed(float elapsed, int length)
    {
        if (length <= 0 || elapsed < 0f)
        {
            return 0;
        }

        float each = System.Math.Min(LetterSeconds, LineSeconds / length);
        int typed = (int)(elapsed / each) + 1;
        return typed >= length ? length : typed;
    }

    private static float Unit(ref uint h)
    {
        h ^= h >> 16;
        h *= 0x7FEB352Du;
        h ^= h >> 15;
        h *= 0x846CA68Bu;
        h ^= h >> 16;
        return (h & 0xFFFF) / 65535f;
    }
}
