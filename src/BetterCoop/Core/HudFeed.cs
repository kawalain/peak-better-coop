using System;

namespace BetterCoop.Core;

/// <summary>
/// The parts of the Carrier's HUD the Rider's screen copies (Shared View):
/// which crosshair shows and whether it is highlighted, the throw charge, and
/// the held rope's length in segments (-1 without a rope).
/// </summary>
internal readonly struct CarrierHud : IEquatable<CarrierHud>
{
    /// <summary>No crosshair, as when the Shared Body is down.</summary>
    public const sbyte NoReticle = -1;

    public CarrierHud(sbyte reticle, bool highlighted, float throwCharge, short ropeSegments)
    {
        Reticle = reticle;
        Highlighted = highlighted;
        ThrowCharge = throwCharge;
        RopeSegments = ropeSegments;
    }

    /// <summary>The crosshair's place in the game's list, or <see cref="NoReticle"/>.</summary>
    public sbyte Reticle { get; }

    public bool Highlighted { get; }

    public float ThrowCharge { get; }

    public short RopeSegments { get; }

    public bool SameShape(CarrierHud other) =>
        Reticle == other.Reticle && Highlighted == other.Highlighted && RopeSegments == other.RopeSegments;

    public bool Equals(CarrierHud other) => SameShape(other) && ThrowCharge.Equals(other.ThrowCharge);

    public override bool Equals(object? obj) => obj is CarrierHud other && Equals(other);

    public override int GetHashCode() => (Reticle, Highlighted, ThrowCharge, RopeSegments).GetHashCode();

    /// <summary>Photon cannot send an sbyte, so the crosshair travels as an int.</summary>
    public object[] Encode() => [(int)Reticle, Highlighted, ThrowCharge, RopeSegments];

    public static CarrierHud? Decode(object? value) =>
        value is object[] { Length: 4 } parts
        && parts[0] is int reticle and >= sbyte.MinValue and <= sbyte.MaxValue
        && parts[1] is bool highlighted
        && parts[2] is float throwCharge
        && parts[3] is short ropeSegments
            ? new CarrierHud((sbyte)reticle, highlighted, throwCharge, ropeSegments)
            : null;
}

/// <summary>
/// Decides when the Carrier sends its HUD to the Rider: a new crosshair or
/// rope length at once and reliably, the throw charge while it moves at a
/// fixed rate, and its end reliably so the Rider's bar never sticks.
/// </summary>
internal sealed class HudFeed
{
    public const float SendInterval = 1f / 30f;

    private CarrierHud? lastSent;

    private float nextSend;

    public bool ShouldSend(CarrierHud hud, float now, out bool reliable)
    {
        reliable = false;
        if (lastSent is not CarrierHud last)
        {
            reliable = true;
        }
        else if (last.Equals(hud))
        {
            return false;
        }
        else if (!last.SameShape(hud) || (hud.ThrowCharge <= 0f && last.ThrowCharge > 0f))
        {
            reliable = true;
        }
        else if (now < nextSend)
        {
            return false;
        }

        lastSent = hud;
        nextSend = now + SendInterval;
        return true;
    }

    /// <summary>Starts over, so the next HUD goes out whole, as for a new partner.</summary>
    public void Reset()
    {
        lastSent = null;
        nextSend = 0f;
    }
}
