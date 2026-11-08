namespace PeakCoop.Core;

/// <summary>What the Rider's client does to bring the Rider in line with the Shared Body.</summary>
internal enum RiderLifeAction
{
    None,
    Die,
    Revive,
    Wake,
}

/// <summary>
/// Shared Life: the Rider lives and dies with the Shared Body. Decided from
/// the current state rather than from death and revival events, which reach
/// the Rider's client from different senders in no fixed order.
/// </summary>
internal static class SharedLifeRules
{
    /// <summary>
    /// How long a Rider revived on their own (a random revival picked them)
    /// stays alive beside a dead Carrier, for the Carrier's revival to arrive.
    /// </summary>
    public const float RevivalGraceSeconds = 3f;

    public static RiderLifeAction Decide(bool carrierDead, bool riderDead, bool riderUnwell, float sinceRiderRevived)
    {
        if (carrierDead)
        {
            return !riderDead && sinceRiderRevived >= RevivalGraceSeconds ? RiderLifeAction.Die : RiderLifeAction.None;
        }

        if (riderDead)
        {
            return RiderLifeAction.Revive;
        }

        return riderUnwell ? RiderLifeAction.Wake : RiderLifeAction.None;
    }
}
