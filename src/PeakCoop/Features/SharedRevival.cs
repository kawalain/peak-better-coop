using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace PeakCoop.Features;

/// <summary>
/// Reviving one member of a Pair revives both: the client that owns the
/// other member revives them at the same spot, and the carry keeper then
/// lifts the Rider back onto the Carrier.
/// </summary>
[HarmonyPatch]
internal static class SharedRevival
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Character), nameof(Character.RPCA_ReviveAtPosition))]
    private static void PartnerRevivesToo(Character __instance, Vector3 position, bool applyStatus, int statueSegment)
    {
        if (!PairRoster.TryGetPartner(__instance, out Character partner)
            || !partner.IsLocal
            || !partner.data.dead)
        {
            return;
        }

        Plugin.Verbose($"{__instance.characterName} was revived; their partner revives with them.");
        partner.photonView.RPC("RPCA_ReviveAtPosition", RpcTarget.All, position, applyStatus, statueSegment);
    }
}
