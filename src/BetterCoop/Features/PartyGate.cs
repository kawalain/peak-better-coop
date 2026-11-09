using System.Collections.Generic;
using HarmonyLib;
using BetterCoop.Core;
using BetterCoop.Net;
using BetterCoop.Ui;
using Photon.Pun;
using PhotonPlayer = Photon.Realtime.Player;

namespace BetterCoop.Features;

/// <summary>
/// Keeps the party at the Airport until it can form Pairs: an even count of
/// players who can pair, unless odd counts are allowed. Unmodded Players
/// depart with the party (ADR 0009), and a client resting in the room leaves
/// the decision to the Troop Leader.
/// </summary>
[HarmonyPatch]
[HarmonyWrapSafe]
internal static class PartyGate
{
    private static PartyGateVerdict Evaluate()
    {
        var members = new List<PartyMember>();
        if (PhotonNetwork.InRoom)
        {
            foreach (PhotonPlayer player in PhotonNetwork.PlayerList)
            {
                members.Add(new PartyMember(KnownPlayers.NameOf(player, player.ActorNumber), player.IsLocal || RoomProtocol.Speaks(player)));
            }
        }

        return PartyGateRules.Evaluate(members, RunRules.AllowOddPlayerCount || DevHooks.SoloPair);
    }

    private static bool AllowDeparture(string source)
    {
        if (RoomProtocol.Resting)
        {
            return true;
        }

        PartyGateVerdict verdict = Evaluate();
        if (verdict.Unmodded.Count > 0)
        {
            Plugin.Log.LogInfo($"Departing with {verdict.Unmodded.Count} Unmodded Player(s): {string.Join(", ", verdict.Unmodded)}.");
        }

        if (verdict.CanDepart)
        {
            return true;
        }

        Plugin.Log.LogInfo($"Departure blocked at {source} with {verdict.PairableCount} players who can pair.");
        if (verdict.OddCountBlocked)
        {
            CoopNotice.Warning(Strings.OddPlayerCount(verdict.PairableCount, verdict.Unmodded.Count));
        }

        return false;
    }

    /// <summary>The boarding pass "start" button of the player at the kiosk.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BoardingPass), nameof(BoardingPass.StartGame))]
    private static bool BeforeBoardingPassStart() => AllowDeparture("boarding pass");

    /// <summary>The host's authoritative check, also covering players without the mod.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AirportCheckInKiosk), nameof(AirportCheckInKiosk.LoadIslandMaster))]
    private static bool BeforeHostLoadsIsland() => AllowDeparture("host island load");
}
