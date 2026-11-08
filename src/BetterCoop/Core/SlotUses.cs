using System;

namespace BetterCoop.Core;

/// <summary>
/// The remaining uses of the Shared Body's three hand slots and back slot, as
/// the Carrier's HUD shows them. Items that burn or spool down (a candle, a
/// lantern, a jetpack) count down only on the holder's client, so the Rider's
/// HUD takes these values from the Carrier. Each slot is a byte of one int:
/// 0 for a slot without a uses bar, otherwise 1 + the fraction in 254 steps.
/// </summary>
internal readonly struct SlotUses : IEquatable<SlotUses>
{
    public const int Count = 4;

    /// <summary>The back slot's index, after the three hand slots.</summary>
    public const int BackSlot = 3;

    private const int Steps = 254;

    public SlotUses(int packed) => Packed = packed;

    public int Packed { get; }

    /// <summary>The slot's remaining fraction (0 to 1), or null when it shows no uses bar.</summary>
    public float? this[int slot]
    {
        get
        {
            int step = (Packed >> Shift(slot)) & 0xFF;
            return step == 0 ? null : (step - 1) / (float)Steps;
        }
    }

    public SlotUses With(int slot, float? fraction)
    {
        int step = fraction is float f ? 1 + (int)Math.Round(Math.Clamp(f, 0f, 1f) * Steps) : 0;
        int shift = Shift(slot);
        return new SlotUses((Packed & ~(0xFF << shift)) | (step << shift));
    }

    /// <summary>Whether <paramref name="fraction"/> already shows as this slot's value.</summary>
    public bool Shows(int slot, float fraction) => With(slot, fraction).Packed == Packed;

    public bool Equals(SlotUses other) => Packed == other.Packed;

    public override bool Equals(object? obj) => obj is SlotUses other && Equals(other);

    public override int GetHashCode() => Packed;

    private static int Shift(int slot) =>
        slot is >= 0 and < Count ? slot * 8 : throw new ArgumentOutOfRangeException(nameof(slot));
}
