using System.Collections.Generic;
using HarmonyLib;
using BetterCoop.Core;
using BetterCoop.Net;
using BetterCoop.Ui;
using Photon.Pun;
using PhotonPlayer = Photon.Realtime.Player;

namespace BetterCoop.Features;

/// <summary>
/// Keeps the party at the Airport until it can form Pairs: an even player
/// count (unless odd counts are allowed) and the same mod version everywhere.
/// </summary>
[HarmonyPatch]
internal static class PartyGate
{
    private static PartyGateVerdict Evaluate()
    {
        var members = new List<PartyMember>();
        if (PhotonNetwork.InRoom)
        {
            foreach (PhotonPlayer player in PhotonNetwork.PlayerList)
            {
                members.Add(new PartyMember(player.NickName, ModPresence.VersionOf(player)));
            }
        }

        return PartyGateRules.Evaluate(
            members,
            MyPluginInfo.PLUGIN_VERSION,
            RunRules.AllowOddPlayerCount || ControlSplit.SoloPairWanted);
    }

    private static bool AllowDeparture(string source)
    {
        PartyGateVerdict verdict = Evaluate();
        if (verdict.CanDepart)
        {
            return true;
        }

        Plugin.Log.LogInfo($"Departure blocked at {source} with {verdict.PlayerCount} players.");

        if (verdict.OddCountBlocked)
        {
            CoopNotice.Warning(Strings.OddPlayerCount(verdict.PlayerCount));
        }

        foreach (string player in verdict.PlayersWithoutMod)
        {
            CoopNotice.Warning(Strings.MissingMod(player));
        }

        foreach (PartyMember member in verdict.PlayersWithOtherVersion)
        {
            CoopNotice.Warning(Strings.OtherVersion(member.Name, member.ModVersion!, MyPluginInfo.PLUGIN_VERSION));
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
