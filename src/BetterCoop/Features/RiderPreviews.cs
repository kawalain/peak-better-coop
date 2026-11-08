using HarmonyLib;
using UnityEngine;
using Zorro.ControllerSupport;
using Zorro.Core;

namespace BetterCoop.Features;

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
[HarmonyWrapSafe]
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
            Pose? camera = RiderView.AimCameraForRider();
            try
            {
                __instance.TryUpdatePreview();
            }
            finally
            {
                RiderView.RestoreCamera(camera);
            }
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
            Pose? camera = RiderView.AimCameraForRider();
            try
            {
                RaycastHit hit = __instance.GetHit(out _);
                __instance.item.overrideUsability = Optionable<bool>.Some(hit.transform);
            }
            finally
            {
                RiderView.RestoreCamera(camera);
            }
        }
    }

    /// <summary>
    /// The rope and vine shooters aim from the camera and fire only from the
    /// ground: for the Rider, the Shared Body's eyes and ground, not the Rider's own.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(RopeShooter), nameof(RopeShooter.WillAttach))]
    private static void GroundRopeShooter(RopeShooter __instance, out StandIn? __state) =>
        __state = StandInForShooter(__instance.item.holderCharacter);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(RopeShooter), nameof(RopeShooter.WillAttach))]
    private static void UngroundRopeShooter(StandIn? __state) => Restore(__state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(VineShooter), nameof(VineShooter.WillAttach))]
    private static void GroundVineShooter(VineShooter __instance, out StandIn? __state) =>
        __state = StandInForShooter(__instance.item.holderCharacter);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(VineShooter), nameof(VineShooter.WillAttach))]
    private static void UngroundVineShooter(StandIn? __state) => Restore(__state);

    /// <summary>What a shooter's check borrowed for the Rider: their own ground, and the camera.</summary>
    internal readonly struct StandIn(bool ownGround, Pose? camera)
    {
        public bool OwnGround { get; } = ownGround;

        public Pose? Camera { get; } = camera;
    }

    /// <summary>The game reads the local character's ground; for the Rider, lend it the Carrier's for one call.</summary>
    private static StandIn? StandInForShooter(Character? holder)
    {
        if (!IsRidersCarrier(holder))
        {
            return null;
        }

        CharacterData rider = Character.localCharacter.data;
        bool own = rider.isGrounded;
        rider.isGrounded = holder!.data.isGrounded;
        return new StandIn(own, RiderView.AimCameraForRider());
    }

    private static void Restore(StandIn? standIn)
    {
        if (standIn is StandIn borrowed)
        {
            Character.localCharacter.data.isGrounded = borrowed.OwnGround;
            RiderView.RestoreCamera(borrowed.Camera);
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
        if (!RiderView.TryGetRiderAim(out Vector3 eyes, out Quaternion aim))
        {
            EndPiton();
            return;
        }

        if (pitonPreview != null)
        {
            float reach = climbing ? spike.climbingSpikePreviewDisableDistance : spike.climbingSpikePreviewDisableDistanceGrounded;
            bool held = pitonWithPrimary ? RiderIntent.Primary : RiderIntent.Secondary;
            pitonTick += Time.deltaTime;
            if (!held
                || Vector3.Distance(eyes, pitonPreview.transform.position) > reach
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

        float distance = climbing ? spike.climbingSpikeStartDistance : spike.climbingSpikeStartDistanceGrounded;
        if (!Physics.Raycast(eyes, aim * Vector3.forward, out RaycastHit hit, distance, HelperFunctions.GetMask(HelperFunctions.LayerType.TerrainMap)))
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
            if (!RiderView.TryGetRiderAim(out Vector3 eyes, out Quaternion aim))
            {
                return;
            }

            Vector3 forward = aim * Vector3.forward;
            RaycastHit hit = HelperFunctions.LineCheck(
                eyes,
                eyes + (forward * tier.maxAnchorGhostDistance),
                HelperFunctions.LayerType.TerrainMap);
            if (hit.collider != null)
            {
                anchorGhost.Ghost = true;
                anchorGhost.transform.position = hit.point;
                anchorGhost.transform.forward = Vector3.Cross(aim * Vector3.right, hit.normal);
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
    private void Update()
    {
        try
        {
            RiderPreviews.Keep();
        }
        catch (System.Exception exception)
        {
            Guard.Report("RiderPreviews.Keep", exception);
        }
    }
}
