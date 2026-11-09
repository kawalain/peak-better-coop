using System;
using BetterCoop.Core;
using BetterCoop.Ui;
using Photon.Pun;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
using PhotonPlayer = Photon.Realtime.Player;

namespace BetterCoop.Net;

/// <summary>
/// The rules of a Run, set by the Troop Leader's config and shared through a
/// room property so every client judges the Run the same way. A player who
/// becomes Troop Leader keeps the room's rules, writing them into their own
/// config, rather than bringing their own.
/// </summary>
internal sealed class RunRules : MonoBehaviourPunCallbacks
{
    [Flags]
    private enum Flags
    {
        None = 0,
        AllowOddPlayerCount = 1,
        CarryWeight = 2,
        CampfireShuffle = 4,
        TimedShuffle = 8,
        HandOverPlace = 16,
        HandOverItems = 32,
        RiderPicksFromBackpack = 64,
        FollowRolePreferences = 128,
        AllowSwapRequests = 256,
        LatePairing = 512,
        RescueCarry = 1024,
    }

    /// <summary>The Timed Shuffle interval's minutes sit above the flags.</summary>
    private const int IntervalShift = 16;

    private const int FlagMask = (1 << IntervalShift) - 1;

    /// <summary>Set while the room's rules are written into the config, so the half-written config is not published.</summary>
    private static bool adopting;

    /// <summary>How long the room's rules must stand before a new Troop Leader trusts them.</summary>
    private const float SteadySeconds = 2f;

    /// <summary>The room's rules as last seen, and since when.</summary>
    private static (int Value, float Since)? seen;

    /// <summary>The last rules that stood in the room for a while, as the Troop Leader puts back any others.</summary>
    private static int? steady;

    /// <summary>How long a change of rules waits for more before the telegram goes out, so a few clicks make one telegram.</summary>
    private const float TellAfterSeconds = 2f;

    /// <summary>The rules before the changes not yet told, and when the last change came.</summary>
    private static (int From, float Last)? untold;

    public static bool AllowOddPlayerCount => Has(Flags.AllowOddPlayerCount);

    public static bool CarryWeight => Has(Flags.CarryWeight);

    public static bool CampfireShuffle => Has(Flags.CampfireShuffle);

    public static bool TimedShuffle => Has(Flags.TimedShuffle);

    public static bool HandOverPlace => Has(Flags.HandOverPlace);

    public static bool HandOverItems => Has(Flags.HandOverItems);

    public static bool RiderPicksFromBackpack => Has(Flags.RiderPicksFromBackpack);

    public static bool FollowRolePreferences => Has(Flags.FollowRolePreferences);

    public static bool AllowSwapRequests => Has(Flags.AllowSwapRequests);

    public static bool LatePairing => Has(Flags.LatePairing);

    public static bool RescueCarry => Has(Flags.RescueCarry);

    /// <summary>Whether a rule is on in the room, as every client reads it.</summary>
    public static bool Is(RunRule rule) => Has(FlagOf(rule));

    /// <summary>The Troop Leader's interval, held to the allowed range whatever arrives.</summary>
    public static double TimedShuffleSeconds => ShuffleInterval.Clamp(Current >> IntervalShift) * 60.0;

    private static int Current
    {
        get
        {
            if (RoomProtocol.TryGet(RoomKeys.Rules, out object value)
                && value is int rules)
            {
                return rules;
            }

            return LocalRules();
        }
    }

    private static bool Has(Flags flag) => (Current & (int)flag) != 0;

    private static Flags FlagOf(RunRule rule) => rule switch
    {
        RunRule.AllowOddPlayerCount => Flags.AllowOddPlayerCount,
        RunRule.CarryWeight => Flags.CarryWeight,
        RunRule.CampfireShuffle => Flags.CampfireShuffle,
        RunRule.TimedShuffle => Flags.TimedShuffle,
        RunRule.HandOverPlace => Flags.HandOverPlace,
        RunRule.HandOverItems => Flags.HandOverItems,
        RunRule.RiderPicksFromBackpack => Flags.RiderPicksFromBackpack,
        RunRule.FollowRolePreferences => Flags.FollowRolePreferences,
        RunRule.AllowSwapRequests => Flags.AllowSwapRequests,
        RunRule.LatePairing => Flags.LatePairing,
        RunRule.RescueCarry => Flags.RescueCarry,
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, null),
    };

    /// <summary>The config entry that holds a rule for the Troop Leader.</summary>
    internal static BepInEx.Configuration.ConfigEntry<bool> Entry(RunRule rule) => rule switch
    {
        RunRule.AllowOddPlayerCount => CoopConfig.AllowOddPlayerCount,
        RunRule.CarryWeight => CoopConfig.CarryWeight,
        RunRule.CampfireShuffle => CoopConfig.CampfireShuffle,
        RunRule.TimedShuffle => CoopConfig.TimedShuffle,
        RunRule.HandOverPlace => CoopConfig.HandOverPlace,
        RunRule.HandOverItems => CoopConfig.HandOverItems,
        RunRule.RiderPicksFromBackpack => CoopConfig.RiderPicksFromBackpack,
        RunRule.FollowRolePreferences => CoopConfig.FollowRolePreferences,
        RunRule.AllowSwapRequests => CoopConfig.AllowSwapRequests,
        RunRule.LatePairing => CoopConfig.LatePairing,
        RunRule.RescueCarry => CoopConfig.RescueCarry,
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, null),
    };

    private static readonly RunRule[] All = (RunRule[])Enum.GetValues(typeof(RunRule));

    private static int LocalRules()
    {
        Flags flags = Flags.None;
        foreach (RunRule rule in All)
        {
            if (Entry(rule).Value)
            {
                flags |= FlagOf(rule);
            }
        }

        return (int)flags | (ShuffleInterval.Clamp(CoopConfig.TimedShuffleInterval.Value) << IntervalShift);
    }

    /// <summary>The least time between two publishes for changed rules: each one reaches every client.</summary>
    private const float PublishSeconds = 0.5f;

    private static bool publishWanted;

    private static float nextPublish;

    private void Start()
    {
        foreach (RunRule rule in All)
        {
            Entry(rule).SettingChanged += (_, _) => WantPublish();
        }

        CoopConfig.TimedShuffleInterval.SettingChanged += (_, _) => WantPublish();
    }

    /// <summary>Rules changed again and again go out as their latest, at most every <see cref="PublishSeconds"/>.</summary>
    private static void WantPublish()
    {
        if (!adopting)
        {
            publishWanted = true;
        }
    }

    public override void OnJoinedRoom()
    {
        try
        {
            if (RoomProtocol.TryGet(RoomKeys.Rules, out object value) && value is int rules)
            {
                See(rules);
            }

            TakeOver();
        }
        catch (Exception exception)
        {
            Guard.Report("RunRules.OnJoinedRoom", exception);
        }
    }

    public override void OnRoomPropertiesUpdate(PhotonHashtable changed)
    {
        if (RoomProtocol.Readable && changed.ContainsKey(RoomKeys.Rules) && changed[RoomKeys.Rules] is int rules)
        {
            int? before = seen?.Value;
            See(rules);
            if (before is int from && from != rules && !PhotonNetwork.IsMasterClient)
            {
                untold = (untold?.From ?? from, Time.realtimeSinceStartup);
            }
        }
    }

    public override void OnLeftRoom()
    {
        seen = null;
        steady = null;
        untold = null;
    }

    private void Update()
    {
        if (publishWanted && Time.unscaledTime >= nextPublish)
        {
            publishWanted = false;
            nextPublish = Time.unscaledTime + PublishSeconds;
            try
            {
                PublishIfHost();
            }
            catch (Exception exception)
            {
                Guard.Report("RunRules.Publish", exception);
            }
        }

        if (untold is not (int from, float last) || Time.realtimeSinceStartup - last < TellAfterSeconds)
        {
            return;
        }

        untold = null;
        try
        {
            if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient && Changes(from, Current) is { Length: > 0 } changes)
            {
                CoopNotice.Info(Strings.RulesChanged(changes));
            }
        }
        catch (Exception exception)
        {
            Guard.Report("RunRules.Update", exception);
        }
    }

    /// <summary>What changed from one set of rules to another, as a line of text: each rule turned on or off, and the new interval.</summary>
    private static string Changes(int from, int to)
    {
        var changes = new System.Collections.Generic.List<string>();
        foreach (RunRule rule in All)
        {
            int flag = (int)FlagOf(rule);
            if ((from & flag) != (to & flag))
            {
                changes.Add((to & flag) != 0 ? Strings.RuleTurnedOn(Strings.RuleName(rule)) : Strings.RuleTurnedOff(Strings.RuleName(rule)));
            }
        }

        int interval = ShuffleInterval.Clamp(to >> IntervalShift);
        if (ShuffleInterval.Clamp(from >> IntervalShift) != interval)
        {
            changes.Add(Strings.RuleSetTo(Strings.RuleTimedShuffleInterval, Strings.Minutes(interval)));
        }

        return string.Join(", ", changes);
    }

    /// <summary>Notes the room's rules; the ones they replace become steady if they stood long enough.</summary>
    private static void See(int rules)
    {
        float now = Time.realtimeSinceStartup;
        if (seen is (int value, float since) && value != rules && now - since >= SteadySeconds)
        {
            steady = value;
        }

        if (seen is not (int same, _) || same != rules)
        {
            seen = (rules, now);
        }
    }

    /// <summary>
    /// The rules a new Troop Leader keeps: the room's, unless they only just
    /// changed, as when another player wrote them a moment before the old
    /// Troop Leader left; then the ones that stood before.
    /// </summary>
    private static int? Trusted(int room)
    {
        See(room);
        return seen is (_, float since) && Time.realtimeSinceStartup - since < SteadySeconds && steady is int before
            ? before
            : room;
    }

    public override void OnMasterClientSwitched(PhotonPlayer newMasterClient)
    {
        try
        {
            TakeOver();
        }
        catch (Exception exception)
        {
            Guard.Report("RunRules.OnMasterClientSwitched", exception);
        }
    }

    /// <summary>
    /// As the Troop Leader: keeps the rules the room already has, written
    /// into this player's config, or brings this player's own to a room that
    /// has none yet.
    /// </summary>
    private static void TakeOver()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        if (RoomProtocol.TryGet(RoomKeys.Rules, out object value)
            && value is int room
            && Trusted(room) is int rules)
        {
            Adopt(rules);
            Plugin.Verbose("Kept the room's Run rules as the new Troop Leader.");
        }

        PublishIfHost();
    }

    private static void Adopt(int rules)
    {
        adopting = true;
        try
        {
            foreach (RunRule rule in All)
            {
                Entry(rule).Value = (rules & (int)FlagOf(rule)) != 0;
            }

            CoopConfig.TimedShuffleInterval.Value = ShuffleInterval.Clamp(rules >> IntervalShift);
        }
        finally
        {
            adopting = false;
        }
    }

    /// <summary>The rules this player's config sets, as the room holds them.</summary>
    internal static int Local => LocalRules();

    private static void PublishIfHost()
    {
        if (adopting || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        int rules = LocalRules();
        RoomWrites.Set(new PhotonHashtable { [RoomKeys.Rules] = rules });
        Plugin.Verbose($"Published Run rules {(Flags)(rules & FlagMask)}, Timed Shuffle every {rules >> IntervalShift} min.");
    }
}
