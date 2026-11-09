using System;

namespace BetterCoop.Core;

/// <summary>
/// How a struck stamp's ink dries back: not evenly, but in patches, as ink
/// soaks into paper unevenly. Every spot of the impression has a wetness from
/// soft layered noise, a little lower toward the rim, where the ink lies
/// thinnest; as the ink dries, the drier spots go first, so the solid breaks
/// into islands that shrink with soft edges until only the outline is left.
/// </summary>
internal static class InkDrying
{
    /// <summary>The size of the smallest patches, in UI units.</summary>
    public const float Patch = 13f;

    /// <summary>How far the rim's thin ink tips a spot toward drying first, 0 to 1.</summary>
    public const float RimBias = 0.3f;

    /// <summary>How soft a patch's edge is, as a span of wetness.</summary>
    public const float Soft = 0.05f;

    private const int Octaves = 3;

    /// <summary>
    /// How wet the spot (<paramref name="x"/>, <paramref name="y"/>) of an
    /// impression <paramref name="width"/> by <paramref name="height"/> UI
    /// units stays, 0 to 1: the order in which its spots dry, drier first.
    /// A new <paramref name="seed"/> soaks in another pattern.
    /// </summary>
    public static float Wetness(float x, float y, float width, float height, int seed)
    {
        float noise = Noise(x / Patch, y / Patch, seed);
        // Out from the middle toward the edge: 0 at the centre, 1 at the rim of the ellipse that fits.
        float dx = width > 0f ? (x / width - 0.5f) * 2f : 0f;
        float dy = height > 0f ? (y / height - 0.5f) * 2f : 0f;
        float rim = Math.Min(1f, (float)Math.Sqrt(dx * dx + dy * dy));
        return Clamp(noise * (1f - RimBias) + (1f - rim) * RimBias);
    }

    /// <summary>
    /// How much ink is left, 0 to 1, at a spot of <paramref name="wetness"/>
    /// once the impression has <paramref name="dried"/> that far, 0 to 1:
    /// all of it before drying starts, none once dried.
    /// </summary>
    public static float Left(float dried, float wetness)
    {
        float level = Clamp(dried) * (1f + 2f * Soft) - Soft;
        return SmoothStep(level - Soft, level + Soft, wetness);
    }

    /// <summary>Soft layered value noise, 0 to 1, spread so its values cover the range fairly evenly.</summary>
    public static float Noise(float x, float y, int seed)
    {
        float sum = 0f;
        float weight = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        for (int octave = 0; octave < Octaves; octave++)
        {
            sum += Value(x * frequency, y * frequency, seed + octave * 1013) * amplitude;
            weight += amplitude;
            amplitude *= 0.5f;
            frequency *= 2.03f;
        }

        // Layered noise bunches round the middle; stretching it lets the patches dry steadily.
        return Clamp((sum / weight - 0.5f) * 2.1f + 0.5f);
    }

    /// <summary>Value noise: a random height at each whole point, eased between them.</summary>
    private static float Value(float x, float y, int seed)
    {
        int x0 = (int)Math.Floor(x);
        int y0 = (int)Math.Floor(y);
        float fx = Fade(x - x0);
        float fy = Fade(y - y0);
        float top = Lerp(Hash(x0, y0, seed), Hash(x0 + 1, y0, seed), fx);
        float bottom = Lerp(Hash(x0, y0 + 1, seed), Hash(x0 + 1, y0 + 1, seed), fx);
        return Lerp(top, bottom, fy);
    }

    /// <summary>A height 0 to 1 for a whole point, the same every time for the same seed.</summary>
    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)seed * 0x9E3779B9u;
            h ^= (uint)x * 0x85EBCA6Bu;
            h = (h << 13) | (h >> 19);
            h ^= (uint)y * 0xC2B2AE35u;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }

    private static float Fade(float t) => t * t * (3f - 2f * t);

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float SmoothStep(float from, float to, float value)
    {
        float t = Clamp((value - from) / (to - from));
        return t * t * (3f - 2f * t);
    }

    private static float Clamp(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
}
