namespace BetterCoop.Net;

/// <summary>Photon custom property keys and event codes owned by the mod.</summary>
internal static class RoomKeys
{
    /// <summary>Player property: the mod version the player runs.</summary>
    public const string Version = "BetterCoop.Version";

    /// <summary>Player property: the player's <see cref="Core.RolePreference"/>.</summary>
    public const string Role = "BetterCoop.Role";

    /// <summary>Room property: the host's Run rules.</summary>
    public const string Rules = "BetterCoop.Rules";

    /// <summary>Room property: the Pairs of the current Run.</summary>
    public const string Pairs = "BetterCoop.Pairs";

    /// <summary>Room property: the latest Pair Shuffle or Role Swap the host announced.</summary>
    public const string Operation = "BetterCoop.Operation";

    /// <summary>Room property: the Body Handover of the latest operation, written with its Pairs.</summary>
    public const string Handover = "BetterCoop.Handover";

    /// <summary>Room property: server time the Timed Shuffle interval counts from.</summary>
    public const string ShuffleClock = "BetterCoop.ShuffleClock";

    /// <summary>Player property: the partner a player asks to swap places with, and when.</summary>
    public const string SwapRequest = "BetterCoop.SwapRequest";

    /// <summary>Event: a Rider's actions, sent to their Carrier.</summary>
    public const byte RiderActionsEvent = 185;

    /// <summary>Event: the host asks every client to repair its Pair.</summary>
    public const byte RecoveryEvent = 186;

    /// <summary>Event: the progress of the Rider's action, sent back by their Carrier.</summary>
    public const byte UseProgressEvent = 187;

    /// <summary>Event: the Carrier's crosshair, throw charge, and rope length, sent to their Rider.</summary>
    public const byte HudEvent = 188;

    /// <summary>Event: an item sound the game played only on the Carrier's client, sent to their Rider.</summary>
    public const byte ItemSoundEvent = 189;
}
