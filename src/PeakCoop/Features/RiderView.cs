using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PeakCoop.Features;

/// <summary>
/// The Rider's screen (Shared View, ADR 0005): the camera sits at the
/// Carrier's head and turns with the Rider's own, locally applied look, and
/// the Shared Body is drawn as on the Carrier's own screen: the Carrier's body
/// like one's own, no back item or backpack pockets, an open Guidebook held up
/// to the eye, and the Rider's own body not at all, as it sits behind the
/// Carrier's eyes.
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

    /// <summary>Renderers hidden for the Rider's screen, to give back when it ends.</summary>
    private static readonly HashSet<Renderer> Hidden = new();

    private static readonly HashSet<Renderer> Wanted = new();

    private static float nextSweep;

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

    /// <summary>The game shows a worn back item to everyone but its wearer; the Rider sees as the wearer.</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CharacterBackpackHandler), "LateUpdate")]
    private static void HideCarrierBackItem(CharacterBackpackHandler __instance)
    {
        if (MainCameraMovement.IsSpectating
            || !TryGetCarrierOfLocalRider(out Character carrier)
            || __instance.character != carrier)
        {
            return;
        }

        __instance.backpackVisuals.SetViewActive(active: false);
        __instance.fannypackVisuals.SetViewActive(active: false);
        __instance.jetpackVisuals.SetViewActive(active: false);
        __instance.rocketpackVisuals.SetViewActive(active: false);
    }

    /// <summary>The game holds an open Guidebook up to its reader's eye; the Rider reads through the Carrier's.</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Guidebook), "LateUpdate")]
    private static void HoldGuidebookForRider(Guidebook __instance)
    {
        if (!__instance.isOpen
            || !TryGetCarrierOfLocalRider(out Character carrier)
            || __instance.holderCharacter != carrier)
        {
            return;
        }

        Transform camera = MainCamera.instance.cam.transform;
        __instance.bookTransform.position = Vector3.Lerp(
            __instance.bookTransform.position,
            camera.position + (camera.forward * __instance.readingDistance),
            Time.deltaTime * 10f);
        __instance.bookTransform.forward = camera.forward;
    }

    /// <summary>
    /// Called every frame after the game's own updates: hides the Rider's body
    /// and the Carrier's backpack pockets on the Rider's screen, and shows
    /// them again once the Rider no longer rides.
    /// </summary>
    internal static void Keep()
    {
        if (Time.unscaledTime < nextSweep)
        {
            return;
        }

        nextSweep = Time.unscaledTime + 0.25f;
        Wanted.Clear();
        if (!MainCameraMovement.IsSpectating && TryGetCarrierOfLocalRider(out Character carrier))
        {
            CollectRiderBody(Character.localCharacter, Wanted);
            CollectPockets(carrier, Wanted);
        }

        foreach (Renderer renderer in Hidden)
        {
            if (renderer != null && !Wanted.Contains(renderer))
            {
                renderer.forceRenderingOff = false;
            }
        }

        Hidden.Clear();
        foreach (Renderer renderer in Wanted)
        {
            renderer.forceRenderingOff = true;
            Hidden.Add(renderer);
        }
    }

    /// <summary>Every renderer of the Rider but the shadow stand-ins the game uses for one's own body.</summary>
    private static void CollectRiderBody(Character rider, HashSet<Renderer> into)
    {
        HideTheBody hide = rider.refs.hideTheBody;
        foreach (Renderer renderer in rider.GetComponentsInChildren<Renderer>(includeInactive: true))
        {
            Transform t = renderer.transform;
            bool shadow = hide != null
                && ((hide.shadowCaster != null && t.IsChildOf(hide.shadowCaster.transform))
                    || (hide.shadowCasterHat != null && t.IsChildOf(hide.shadowCasterHat.transform)));
            if (!shadow)
            {
                into.Add(renderer);
            }
        }
    }

    /// <summary>Items in the Carrier's backpack pockets, which the game hides only for the wearer.</summary>
    private static void CollectPockets(Character carrier, HashSet<Renderer> into)
    {
        CharacterBackpackHandler handler = carrier.refs.backpackHandler;
        if (handler == null)
        {
            return;
        }

        foreach (BackpackOnBackVisuals visuals in new[] { handler.backpackVisuals, handler.fannypackVisuals, handler.jetpackVisuals, handler.rocketpackVisuals })
        {
            if (visuals == null)
            {
                continue;
            }

            foreach (Item item in visuals.spawnedVisualItems.Values)
            {
                if (item != null)
                {
                    into.UnionWith(item.GetComponentsInChildren<Renderer>(includeInactive: true));
                }
            }
        }
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
