using ExitGames.Client.Photon;
using HarmonyLib;
using BetterCoop.Core;
using BetterCoop.Net;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace BetterCoop.Features;

/// <summary>
/// The Shared Body's crosshair, throw bar, rope length, and held-item screen
/// effects on the Rider's screen (Shared View, ADR 0005). The game draws them
/// from the local character, which on the Rider's client is the Rider; the
/// Carrier's client sends what its own HUD shows, and the Rider's draws it.
/// A shooting crosshair depends on aim, so the Rider's client judges it.
/// </summary>
[HarmonyPatch]
internal static class SharedViewHud
{
    private static readonly HudFeed Feed = new();

    private static readonly ProgressSmoother RiderThrow = new();

    /// <summary>The Carrier's throw charge as the Rider's throw bar shows it.</summary>
    internal static float RiderThrowShown => RiderThrow.Display(Time.unscaledTime);

    private static CarrierHud received = new(0, false, 0f, -1);

    private static GameObject? shownReticle;

    private static bool feeding;

    private static int sentTo;

    private static Character? receivedFrom;

    /// <summary>Called every frame on the Carrier's client, after its HUD has updated.</summary>
    private static void SendFromCarrier(GUIManager gui)
    {
        Character local = Character.localCharacter;
        if (local == null
            || PairRoster.RoleOf(local) != PairRole.Carrier
            || !PairRoster.TryGetPartner(local, out Character rider))
        {
            Feed.Reset();
            return;
        }

        if (PairRoster.ActorOf(rider) != sentTo)
        {
            // A new Rider gets the whole HUD.
            sentTo = PairRoster.ActorOf(rider);
            Feed.Reset();
        }

        int reticle = gui.lastReticle == null ? CarrierHud.NoReticle : gui.reticleList.IndexOf(gui.lastReticle);
        Item? item = local.data.currentItem;
        short rope = item != null && item.TryGetComponent(out RopeSpool spool) && spool.rope != null
            ? (short)spool.rope.GetRopeSegments().Count
            : (short)-1;
        var hud = new CarrierHud(
            (sbyte)reticle,
            gui.reticleDefaultImage.color == gui.reticleColorHighlight,
            local.refs.items.throwChargeLevel,
            rope);
        if (!Feed.ShouldSend(hud, Time.unscaledTime, out bool reliable))
        {
            return;
        }

        PhotonNetwork.RaiseEvent(
            RoomKeys.HudEvent,
            hud.Encode(),
            new RaiseEventOptions { TargetActors = [PairRoster.ActorOf(rider)] },
            reliable ? SendOptions.SendReliable : SendOptions.SendUnreliable);
    }

    internal static void Receive(EventData photonEvent)
    {
        if (photonEvent.Code != RoomKeys.HudEvent
            || CarrierHud.Decode(photonEvent.CustomData) is not CarrierHud hud
            || RiderHud.ObservedCarrier() is not Character carrier
            || photonEvent.Sender != PairRoster.ActorOf(carrier))
        {
            return;
        }

        received = hud;
        RiderThrow.Receive(hud.ThrowCharge, Time.unscaledTime);
    }

    /// <summary>The Carrier's client sends; the Rider's draws the Carrier's crosshair instead of the Rider's own.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GUIManager), "UpdateReticle")]
    private static bool ShowCarrierReticle(GUIManager __instance)
    {
        if (RiderHud.ObservedCarrier() is not Character carrier)
        {
            shownReticle = null;
            return true;
        }

        if (carrier != receivedFrom)
        {
            receivedFrom = carrier;
            received = new CarrierHud(0, false, 0f, -1);
            RiderThrow.Reset();
        }

        __instance.reticleDefaultImage.color = received.Highlighted ? __instance.reticleColorHighlight : __instance.reticleColorDefault;
        GameObject? reticle = received.Reticle >= 0 && received.Reticle < __instance.reticleList.Count
            ? __instance.reticleList[received.Reticle]
            : null;

        // Whether a held shooter would hit depends on aim: judge it here.
        Item? item = carrier.data.currentItem;
        if (reticle == __instance.reticleDefault || reticle == __instance.reticleShoot)
        {
            bool shoot = item != null && item.UIData.isShootable && item.CanUsePrimary();
            reticle = shoot ? __instance.reticleShoot : __instance.reticleDefault;
            if (shoot)
            {
                __instance.reticleShootImage.color = item!.raycastingCharacter ? __instance.reticleColorHighlight : __instance.reticleColorDefault;
            }
        }

        if (reticle != shownReticle && reticle == __instance.reticleGrasp)
        {
            __instance.reticleGrasp.GetComponent<Animator>().Play("Play", 0, 0f);
        }

        shownReticle = reticle;
        __instance.SetReticle(reticle);
        return false;
    }

    /// <summary>The Rider's throw bar shows the Carrier's charge, smoothed between updates.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GUIManager), "UpdateThrow")]
    private static bool ShowCarrierThrow(GUIManager __instance)
    {
        if (RiderHud.ObservedCarrier() == null)
        {
            RiderThrow.Reset();
            return true;
        }

        float charge = RiderThrow.Display(Time.unscaledTime);
        __instance.throwGO.SetActive(charge > 0f);
        if (charge > 0f)
        {
            __instance.throwBar.fillAmount = Mathf.Lerp(0.692f, 0.808f, charge);
            __instance.throwBar.color = __instance.throwGradient.Evaluate(charge);
        }

        return false;
    }

    /// <summary>The Rider's rope gauge shows the length of the rope the Carrier holds.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GUIManager), "UpdateRope")]
    private static bool ShowCarrierRope(GUIManager __instance)
    {
        if (RiderHud.ObservedCarrier() is not Character carrier)
        {
            return true;
        }

        Item? item = carrier.data.currentItem;
        if (item == null || !item.TryGetComponent(out RopeSpool spool))
        {
            __instance.ui_rope.gameObject.SetActive(false);
            return false;
        }

        __instance.ui_rope.gameObject.SetActive(true);
        if (received.RopeSegments >= 0)
        {
            __instance.ui_rope.UpdateRope(received.RopeSegments);
        }

        Shader.SetGlobalFloat(__instance.ROPE_INVERT, spool.isAntiRope ? 1 : 0);
        return false;
    }

    /// <summary>Sends the Carrier's HUD once the game has drawn it this frame.</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GUIManager), "LateUpdate")]
    private static void SendAfterHud(GUIManager __instance) => SendFromCarrier(__instance);

    /// <summary>
    /// Items that shake the screen or warp the view as they are used read
    /// their own cast progress, which only the holder's client keeps; on the
    /// Rider's client they read the Shared Body's progress for the update.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ItemVFX), "Update")]
    private static void FeedItemVfx(ItemVFX __instance, out (float Progress, bool Using)? __state) =>
        __state = LendProgress(__instance.item);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(ItemVFX), "Update")]
    private static void UnfeedItemVfx(ItemVFX __instance, (float Progress, bool Using)? __state) =>
        ReturnProgress(__instance.item, __state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(WarpCompassVFX), "Update")]
    private static void FeedWarpCompass(WarpCompassVFX __instance, out (float Progress, bool Using)? __state) =>
        __state = LendProgress(__instance.item);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(WarpCompassVFX), "Update")]
    private static void UnfeedWarpCompass(WarpCompassVFX __instance, (float Progress, bool Using)? __state) =>
        ReturnProgress(__instance.item, __state);

    private static (float Progress, bool Using)? LendProgress(Item? item)
    {
        if (feeding
            || item == null
            || !RiderView.TryGetCarrierOfLocalRider(out Character carrier)
            || item.holderCharacter != carrier)
        {
            return null;
        }

        float progress = UseFeedback.RiderProgressShown;
        var own = (item.castProgress, item.isUsingPrimary);
        feeding = true;
        item.castProgress = progress;
        item.isUsingPrimary = progress > 0f;
        return own;
    }

    private static void ReturnProgress(Item? item, (float Progress, bool Using)? own)
    {
        if (own is not (float progress, bool usingPrimary) || item == null)
        {
            return;
        }

        item.castProgress = progress;
        item.isUsingPrimary = usingPrimary;
        feeding = false;
    }
}
