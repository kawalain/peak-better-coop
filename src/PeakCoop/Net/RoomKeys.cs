namespace PeakCoop.Net;

/// <summary>Photon custom property keys and event codes owned by the mod.</summary>
internal static class RoomKeys
{
    /// <summary>Player property: the mod version the player runs.</summary>
    public const string Version = "PeakCoop.Version";

    /// <summary>Player property: the player's <see cref="Core.RolePreference"/>.</summary>
    public const string Role = "PeakCoop.Role";

    /// <summary>Room property: the host's Run rules.</summary>
    public const string Rules = "PeakCoop.Rules";

    /// <summary>Room property: the Pairs of the current Run.</summary>
    public const string Pairs = "PeakCoop.Pairs";

    /// <summary>Event: a Rider's actions, sent to their Carrier.</summary>
    public const byte RiderActionsEvent = 185;

    /// <summary>Event: the host asks every client to repair its Pair.</summary>
    public const byte RecoveryEvent = 186;

    /// <summary>Event: the progress of the Rider's action, sent back by their Carrier.</summary>
    public const byte UseProgressEvent = 187;
}
