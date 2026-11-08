using System;

namespace BetterCoop.Core;

/// <summary>
/// The remaining uses of the Shared Body's slots as the Carrier's HUD shows
/// them, with how fast each is changing. Items that burn, toot, or spool
/// down count down on the holder's client only, so the Rider's HUD draws
/// them from these values. A value is the fraction left (0 to 1), or
/// <see cref="None"/> for a slot without a uses bar; a rate is in fractions
/// per second.
/// </summary>
internal readonly struct SlotUses
{
    /// <summary>Hand slots 0 to 2, then the back slot, then the temporary slot.</summary>
    public const int Count = 5;

    public const int BackSlot = 3;

    public const int TemporarySlot = 4;

    /// <summary>A slot without a uses bar: empty, or an item that has none.</summary>
    public const float None = -1f;

    public SlotUses(float[] values, float[] rates)
    {
        Values = values;
        Rates = rates;
    }

    public float[] Values { get; }

    public float[] Rates { get; }

    public object[] Encode() => [Values, Rates];

    public static SlotUses? Decode(object? payload)
    {
        if (payload is not object[] { Length: 2 } parts
            || parts[0] is not float[] { Length: Count } values
            || parts[1] is not float[] { Length: Count } rates)
        {
            return null;
        }

        for (int slot = 0; slot < Count; slot++)
        {
            bool valid = values[slot] == None || (values[slot] >= 0f && values[slot] <= 1f);
            if (!valid || float.IsNaN(rates[slot]) || float.IsInfinity(rates[slot]))
            {
                return null;
            }
        }

        return new SlotUses(values, rates);
    }
}

/// <summary>
/// Decides when the Carrier sends its slots' remaining uses: at most every
/// <see cref="SendInterval"/>, and only when a slot gained or lost its bar,
/// moved by a visible step, or started or stopped moving. Each send carries
/// the rate since the last one, so the Rider's bar keeps moving between
/// sends, and a use that stops sends once more with a rate of zero.
/// </summary>
internal sealed class UsesFeed
{
    public const float SendInterval = 0.1f;

    /// <summary>The smallest change worth sending.</summary>
    public const float Step = 1f / 512f;

    /// <summary>Sends further apart than this carry no rate: the change came after a pause.</summary>
    public const float RateWindow = 0.5f;

    /// <summary>A jump larger than this is another item in the slot, not a use.</summary>
    public const float SwapJump = 0.5f;

    private float[]? sentValues;

    private float[] sentRates = new float[SlotUses.Count];

    private float sentAt;

    public bool ShouldSend(float[] values, float now, out SlotUses message)
    {
        message = default;
        if (values.Length != SlotUses.Count)
        {
            throw new ArgumentException($"Expected {SlotUses.Count} slots.", nameof(values));
        }

        float[] rates = new float[SlotUses.Count];
        if (sentValues != null)
        {
            if (now - sentAt < SendInterval)
            {
                return false;
            }

            float elapsed = now - sentAt;
            bool changed = false;
            for (int slot = 0; slot < SlotUses.Count; slot++)
            {
                float value = values[slot];
                float before = sentValues[slot];
                bool present = value != SlotUses.None;
                if (present != (before != SlotUses.None))
                {
                    changed = true;
                    continue;
                }

                if (!present)
                {
                    continue;
                }

                float delta = value - before;
                if (elapsed <= RateWindow && Math.Abs(delta) <= SwapJump)
                {
                    rates[slot] = delta / elapsed;
                }

                if (Math.Abs(delta) >= Step || (rates[slot] == 0f) != (sentRates[slot] == 0f))
                {
                    changed = true;
                }
            }

            if (!changed)
            {
                return false;
            }
        }

        sentValues = (float[])values.Clone();
        sentRates = rates;
        sentAt = now;
        message = new SlotUses(sentValues, rates);
        return true;
    }

    /// <summary>Starts over, so the next values go out at once, as for a new partner.</summary>
    public void Reset()
    {
        sentValues = null;
        sentRates = new float[SlotUses.Count];
        sentAt = 0f;
    }
}
