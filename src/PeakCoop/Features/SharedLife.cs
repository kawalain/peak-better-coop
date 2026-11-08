using HarmonyLib;
using PeakCoop.Core;
using Photon.Pun;
using UnityEngine;

namespace PeakCoop.Features;

/// <summary>
/// Shared Life: the Rider lives and dies with the Shared Body. The Rider's
/// client follows the Carrier's state (<see cref="SharedLifeRules"/>); the
/// patches below only make the common cases immediate.
/// </summary>
[HarmonyPatch]
internal static class SharedLife
{
    private const float RetrySeconds = 1f;

    private static float riderRevivedAt = float.NegativeInfinity;

    private static float nextAttempt;

    /// <summary>Called every frame; acts only for a local Rider.</summary>
    internal static void Keep()
    {
        Character rider = Character.localCharacter;
        if (rider == null
            || Time.time < nextAttempt
            || PairRoster.RoleOf(rider) != PairRole.Rider
            || !PairRoster.TryGetPartner(rider, out Character carrier))
        {
            return;
        }

        bool unwell = rider.data.passedOut || rider.data.fullyPassedOut || rider.data.petrifyAmount > 0;
        RiderLifeAction action = SharedLifeRules.Decide(
            carrier.data.dead,
            rider.data.dead,
            unwell,
            Time.time - riderRevivedAt);
        if (action == RiderLifeAction.None)
        {
            return;
        }

        nextAttempt = Time.time + RetrySeconds;
        Plugin.Verbose($"Shared Life: Rider {action} to match the Carrier.");
        switch (action)
        {
            case RiderLifeAction.Die:
                rider.DieInstantly();
                break;
            case RiderLifeAction.Revive:
                rider.photonView.RPC("RPCA_ReviveAtPosition", RpcTarget.All, carrier.Center + Vector3.up, false, -1);
                break;
            case RiderLifeAction.Wake:
                CarryKeeper.WakeRider(rider);
                break;
        }
    }

    /// <summary>The Rider dies when the Carrier dies, at once rather than at the next check.</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Character), nameof(Character.RPCA_Die))]
    private static void RiderDiesWithCarrier(Character __instance)
    {
        if (PairRoster.RoleOf(__instance) == PairRole.Carrier
            && PairRoster.TryGetPartner(__instance, out Character rider)
            && rider.IsLocal
            && !rider.data.dead)
        {
            Plugin.Verbose($"{__instance.characterName} died; their Rider dies with them.");
            rider.DieInstantly();
        }
    }

    /// <summary>The Rider never dies alone, so the Carrier never dies for the Rider.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Character), nameof(Character.DieInstantly))]
    private static bool RiderDiesOnlyWithCarrier(Character __instance) => !IsRiderBesideLiveCarrier(__instance);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Character), nameof(Character.DieInstantlyLocal))]
    private static bool RiderDiesOnlyWithCarrierLocal(Character __instance) => !IsRiderBesideLiveCarrier(__instance);

    /// <summary>
    /// Reviving either member revives both at the same spot; a Rider picked
    /// by a random revival gets time for the Carrier's revival to arrive.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Character), nameof(Character.RPCA_ReviveAtPosition))]
    private static void PartnerRevivesToo(Character __instance, Vector3 position, bool applyStatus, int statueSegment)
    {
        if (__instance.IsLocal && PairRoster.RoleOf(__instance) == PairRole.Rider)
        {
            riderRevivedAt = Time.time;
        }

        if (!PairRoster.TryGetPartner(__instance, out Character partner)
            || !partner.IsLocal
            || !partner.data.dead)
        {
            return;
        }

        Plugin.Verbose($"{__instance.characterName} was revived; their partner revives with them.");
        partner.photonView.RPC("RPCA_ReviveAtPosition", RpcTarget.All, position, applyStatus, statueSegment);
    }

    private static bool IsRiderBesideLiveCarrier(Character character)
    {
        if (!character.IsLocal
            || PairRoster.RoleOf(character) != PairRole.Rider
            || !PairRoster.TryGetPartner(character, out Character carrier)
            || carrier.data.dead)
        {
            return false;
        }

        Plugin.Verbose("Refused a Rider's death while the Carrier lives.");
        return true;
    }
}
