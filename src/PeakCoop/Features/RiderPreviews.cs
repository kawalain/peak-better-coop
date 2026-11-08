using HarmonyLib;
using UnityEngine;
using Zorro.ControllerSupport;
using Zorro.Core;

namespace PeakCoop.Features;

/// <summary>
/// What the Rider presses this frame, before the Control Split sends it to
/// the Carrier's client: previews on the Rider's screen follow it at once.
/// </summary>
internal static class RiderIntent
{
    internal static bool Primary { get; private set; }

    internal static bool Secondary { get; private set; }

    internal static float Scroll { get; private set; }

    internal static bool ScrollForward { get; private set; }

    internal static bool ScrollBackward { get; private set; }

    internal static void Capture(CharacterInput input)
    {
        Primary = input.usePrimaryIsPressed;
        Secondary = input.useSecondaryIsPressed;
        Scroll = input.scrollInput;
        ScrollForward = input.scrollForwardWasPressed;
        ScrollBackward = input.scrollBackwardWasPressed;
    }

    internal static void Clear()
    {
        Primary = Secondary = ScrollForward = ScrollBackward = false;
        Scroll = 0f;
    }
}

/// <summary>
/// Placement previews and aim checks of the Shared Body's held item on the
/// Rider's screen (Shared View, ADR 0005). The game draws them only for the
/// holder's own client; the Rider aims, so the Rider's client draws them from
/// the Rider's own look, with the same rules the game applies for the holder.
/// The Carrier's client still places the item, from the look it receives.
/// </summary>
[HarmonyPatch]
internal static class RiderPreviews
{
    private static GameObject? pitonPreview;

    private static bool pitonWithPrimary;

    private static float pitonTick;

    private static RopeAnchor? anchorGhost;

    private static RaycastHit? anchorPlace;

    private static float anchorTime;

    private static bool anchorReleaseCheck;

    private static bool IsRidersCarrier(Character? holder) =>
        holder != null && RiderView.TryGetCarrierOfLocalRider(out Character carrier) && holder == carrier;

    /// <summary>Scout Cannon, effigy, and checkpoint previews follow the Rider's aim.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Constructable), "Update")]
    private static bool PreviewConstructable(Constructable __instance)
    {
        if (!IsRidersCarrier(__instance.item.holderCharacter))
        {
            return true;
        }

        // The Carrier's client holds the spot still while building.
        if (!UseFeedback.RiderSeesUse)
        {
            __instance.TryUpdatePreview();
        }

        if (__instance.isAngleable && __instance.currentPreview != null)
        {
            TurnConstructable(__instance);
        }

        __instance.item.overrideUsability = __instance.valid ? Optionable<bool>.None : Optionable<bool>.Some(false);
        return false;
    }

    /// <summary>The Scout Cannon's angle turns with the Rider's scroll, as it does on the Carrier's client.</summary>
    private static void TurnConstructable(Constructable constructable)
    {
        Action_ConstructableScoutCannonScroll? scroll = constructable.item.GetComponentInChildren<Action_ConstructableScoutCannonScroll>();
        if (scroll == null)
        {
            return;
        }

        if (RiderIntent.ScrollBackward)
        {
            scroll.Scrolled(-1f);
        }

        if (RiderIntent.ScrollForward)
        {
            scroll.Scrolled(1f);
        }

        if (RiderIntent.Scroll != 0f && InputHandler.GetCurrentUsedInputScheme() == InputScheme.KeyboardMouse)
        {
            scroll.Scrolled(RiderIntent.Scroll);
        }
    }

    /// <summary>The rescue hook knows on the Rider's screen whether it would catch something, and whom.</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RescueHook), nameof(RescueHook.Update))]
    private static void AimRescueHook(RescueHook __instance)
    {
        if (IsRidersCarrier(__instance.item.holderCharacter))
        {
            RaycastHit hit = __instance.GetHit(out _);
            __instance.item.overrideUsability = Optionable<bool>.Some(hit.transform);
        }
    }

    /// <summary>The rope and vine shooters fire only from the ground: the Shared Body's, not the Rider's own.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(RopeShooter), nameof(RopeShooter.WillAttach))]
    private static void GroundRopeShooter(RopeShooter __instance, out bool? __state) =>
        __state = StandInForGround(__instance.item.holderCharacter);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(RopeShooter), nameof(RopeShooter.WillAttach))]
    private static void UngroundRopeShooter(bool? __state) => RestoreGround(__state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(VineShooter), nameof(VineShooter.WillAttach))]
    private static void GroundVineShooter(VineShooter __instance, out bool? __state) =>
        __state = StandInForGround(__instance.item.holderCharacter);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(VineShooter), nameof(VineShooter.WillAttach))]
    private static void UngroundVineShooter(bool? __state) => RestoreGround(__state);

    /// <summary>The game reads the local character's ground; for the Rider, lend it the Carrier's for one call.</summary>
    private static bool? StandInForGround(Character? holder)
    {
        if (!IsRidersCarrier(holder))
        {
            return null;
        }

        CharacterData rider = Character.localCharacter.data;
        bool own = rider.isGrounded;
        rider.isGrounded = holder!.data.isGrounded;
        return own;
    }

    private static void RestoreGround(bool? own)
    {
        if (own is bool grounded)
        {
            Character.localCharacter.data.isGrounded = grounded;
        }
    }

    /// <summary>Called every frame: previews the game draws from the holder's own input.</summary>
    internal static void Keep()
    {
        Character? carrier = RiderView.TryGetCarrierOfLocalRider(out Character c) ? c : null;
        KeepPiton(carrier);
        KeepRopeAnchor(carrier);
    }

    /// <summary>A piton's spot shows while the Rider hammers it, as the game does for the one hammering.</summary>
    private static void KeepPiton(Character? carrier)
    {
        CharacterItems? items = carrier != null ? carrier.refs.items : null;
        if (items != null && items.currentClimbingSpikeComponent == null)
        {
            items.UpdateClimbingSpikeCount(carrier!.player.itemSlots);
        }

        ClimbingSpikeComponent? spike = items != null ? items.currentClimbingSpikeComponent : null;
        ItemSlot? slot = items != null ? items.currentClimbingSpikeItemSlot : null;
        if (carrier == null || items == null || spike == null || slot?.prefab == null || carrier.data.climbingSpikeCount <= 0)
        {
            EndPiton();
            return;
        }

        bool climbing = carrier.data.isClimbingAnything;
        if (pitonPreview != null)
        {
            float reach = climbing ? spike.climbingSpikePreviewDisableDistance : spike.climbingSpikePreviewDisableDistanceGrounded;
            bool held = pitonWithPrimary ? RiderIntent.Primary : RiderIntent.Secondary;
            pitonTick += Time.deltaTime;
            if (!held
                || Vector3.Distance(MainCamera.instance.transform.position, pitonPreview.transform.position) > reach
                || pitonTick >= slot.prefab.usingTimePrimary)
            {
                EndPiton();
            }

            return;
        }

        bool selected = items.climbingSpikeSelected;
        bool primary = RiderIntent.Primary && !climbing && selected;
        bool secondary = !primary && RiderIntent.Secondary && (selected || climbing);
        if (!primary && !secondary)
        {
            return;
        }

        Transform camera = MainCamera.instance.transform;
        float distance = climbing ? spike.climbingSpikeStartDistance : spike.climbingSpikeStartDistanceGrounded;
        if (!Physics.Raycast(camera.position, camera.forward, out RaycastHit hit, distance, HelperFunctions.GetMask(HelperFunctions.LayerType.TerrainMap)))
        {
            return;
        }

        pitonWithPrimary = primary;
        pitonTick = Time.deltaTime;
        pitonPreview = Object.Instantiate(spike.climbingSpikePreviewPrefab);
        pitonPreview.transform.position = hit.point;
        pitonPreview.transform.rotation = Quaternion.LookRotation(-hit.normal, Vector3.up);
    }

    private static void EndPiton()
    {
        if (pitonPreview != null)
        {
            Object.Destroy(pitonPreview);
        }

        pitonPreview = null;
        pitonTick = 0f;
    }

    /// <summary>A rope anchor's ghost follows the Rider's aim until it finds a spot in reach, as the game does for the one tying it.</summary>
    private static void KeepRopeAnchor(Character? carrier)
    {
        RopeTier? tier = carrier != null && carrier.data.currentItem != null
            ? carrier.data.currentItem.GetComponent<RopeTier>()
            : null;
        if (tier == null)
        {
            EndRopeAnchor();
            anchorReleaseCheck = false;
            return;
        }

        if (anchorReleaseCheck)
        {
            anchorReleaseCheck = RiderIntent.Primary;
            return;
        }

        if (!RiderIntent.Primary
            || (anchorGhost != null && anchorPlace.HasValue
                && Vector3.Distance(anchorPlace.Value.point, tier.transform.position) > tier.maxAnchorGhostDistance))
        {
            EndRopeAnchor();
            return;
        }

        if (anchorGhost == null)
        {
            anchorGhost = Object.Instantiate(tier.anchorPreview).GetComponent<RopeAnchor>();
            anchorGhost.anchorPoint.gameObject.SetActive(false);
            anchorPlace = null;
            anchorTime = 0f;
        }

        if (!anchorPlace.HasValue)
        {
            Transform camera = MainCamera.instance.transform;
            RaycastHit hit = HelperFunctions.LineCheck(
                camera.position,
                camera.position + (camera.forward * tier.maxAnchorGhostDistance),
                HelperFunctions.LayerType.TerrainMap);
            if (hit.collider != null)
            {
                anchorGhost.Ghost = true;
                anchorGhost.transform.position = hit.point;
                anchorGhost.transform.forward = Vector3.Cross(camera.right, hit.normal);
                anchorGhost.transform.up = hit.normal;
                if (Vector3.Distance(hit.point, carrier!.Center) < tier.maxAnchorDistance)
                {
                    anchorPlace = hit;
                    anchorGhost.Ghost = false;
                }
            }

            return;
        }

        anchorTime += Time.deltaTime;
        if (anchorTime >= tier.castTime)
        {
            EndRopeAnchor();
            anchorReleaseCheck = true;
        }
    }

    private static void EndRopeAnchor()
    {
        if (anchorGhost != null)
        {
            Object.DestroyImmediate(anchorGhost.gameObject);
        }

        anchorGhost = null;
        anchorPlace = null;
        anchorTime = 0f;
    }
}

/// <summary>Drives the Rider's previews.</summary>
internal sealed class RiderPreviewsKeeper : MonoBehaviour
{
    private void Update() => RiderPreviews.Keep();
}
