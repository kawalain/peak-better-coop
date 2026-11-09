namespace BetterCoop.Core;

/// <summary>Where a scout stands for a Rescue Carry: in no Pair, or as a Pair's Carrier or Rider.</summary>
internal enum RescueStanding
{
    Unpaired,
    Carrier,
    Rider,
}

/// <summary>
/// The Pairs a Rescue Carry involves, by Carrier and Rider actor: the
/// rescuer's and the Rescued's, null for one in no Pair. A rescue lasts
/// only while both stay as they were when it began.
/// </summary>
internal readonly struct RescueHold
{
    public RescueHold((int Carrier, int Rider)? rescuerPair, (int Carrier, int Rider)? rescuedPair)
    {
        RescuerPair = rescuerPair;
        RescuedPair = rescuedPair;
    }

    public (int Carrier, int Rider)? RescuerPair { get; }

    public (int Carrier, int Rider)? RescuedPair { get; }

    public bool Same(RescueHold other) => RescuerPair == other.RescuerPair && RescuedPair == other.RescuedPair;
}

/// <summary>Rescue Carry (ADR 0010): who may be rescued, and when the Rescued is put down.</summary>
internal static class RescueRules
{
    /// <summary>The weight the game gives a carrier for one carried scout (8 units of 0.025).</summary>
    public const float RescuedWeight = 0.2f;

    /// <summary>
    /// Whether a scout can be picked up in a Rescue Carry: passed out, alive,
    /// carried by nobody, and not a Rider, who is never passed out alone and
    /// comes along with their Shared Body.
    /// </summary>
    public static bool CanBeRescued(bool fullyPassedOut, bool dead, bool carried, RescueStanding standing) =>
        fullyPassedOut && !dead && !carried && standing != RescueStanding.Rider;

    /// <summary>
    /// Whether the rescuer keeps carrying the Rescued: the rule still on, the
    /// rescuing Shared Body awake and alive, no Pair change coming, and both
    /// Pairs as they were when the rescue began.
    /// </summary>
    public static bool Keeps(RescueHold began, RescueHold now, bool ruleOn, bool sharedBodyUp, bool pairsChanging) =>
        ruleOn && sharedBodyUp && !pairsChanging && began.Same(now);
}
