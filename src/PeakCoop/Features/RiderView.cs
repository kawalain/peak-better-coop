using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PeakCoop.Features;

/// <summary>
/// The Rider's screen: the camera sits at the Carrier's head and turns with
/// the Rider's own, locally applied look, and the Carrier's body is drawn the
/// way the game draws a player's own body (ADR 0002).
/// </summary>
[HarmonyPatch]
internal static class RiderView
{
    /// <summary>The local Rider's Carrier while the carry holds.</summary>
    internal static bool TryGetCarrierOfLocalRider(out Character carrier)
    {
        carrier = null!;
        Character local = Character.localCharacter;
        if (local == null
            || PairRoster.RoleOf(local) != PairRole.Rider
            || !PairRoster.TryGetPartner(local, out Character partner)
            || local.data.carrier != partner)
        {
            return false;
        }

        carrier = partner;
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Character), nameof(Character.GetCameraPos))]
    private static void SeeFromCarrierHead(Character __instance, float forwardOffset, ref Vector3 __result)
    {
        if (__instance.IsLocal && TryGetCarrierOfLocalRider(out Character carrier))
        {
            __result = carrier.GetCameraPos(forwardOffset);
        }
    }

    /// <summary>On the Rider's screen the Carrier's body is ghosted like one's own.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HideTheBody), "Update")]
    private static bool GhostCarrierForRider(HideTheBody __instance)
    {
        Character character = __instance.character;
        if (character == null
            || !TryGetCarrierOfLocalRider(out Character carrier)
            || character != carrier)
        {
            return true;
        }

        if (__instance.isShowing)
        {
            __instance.Toggle(show: false);
        }

        return false;
    }

    /// <summary>
    /// The interaction ray never lands on the partner: the Rider's starts
    /// inside the Carrier's head, and the Carrier's can catch the Rider on
    /// their back. Skip the partner's colliders as the game skips one's own.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Interaction), nameof(Interaction.DoInteractableRaycasts))]
    private static void IgnorePartnerColliders(out int __state)
    {
        __state = 0;
        Character local = Character.localCharacter;
        if (local == null || !PairRoster.TryGetPartner(local, out Character partner)
            || (local.data.carrier != partner && local.data.carriedPlayer != partner))
        {
            return;
        }

        List<Collider> own = local.refs.ragdoll.colliderList;
        List<Collider> partnerColliders = partner.refs.ragdoll.colliderList;
        own.AddRange(partnerColliders);
        __state = partnerColliders.Count;
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Interaction), nameof(Interaction.DoInteractableRaycasts))]
    private static void RestoreOwnColliders(int __state)
    {
        if (__state > 0)
        {
            List<Collider> own = Character.localCharacter.refs.ragdoll.colliderList;
            own.RemoveRange(own.Count - __state, __state);
        }
    }
}
