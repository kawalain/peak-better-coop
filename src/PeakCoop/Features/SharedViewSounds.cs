using System.Collections.Generic;
using ExitGames.Client.Photon;
using HarmonyLib;
using Peak;
using PeakCoop.Net;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace PeakCoop.Features;

/// <summary>
/// Item sounds the game plays only on the holder's client reach the Rider
/// too (Shared View, ADR 0005). While the Carrier's client runs one of the
/// game's owner-only item steps for the Shared Body, every sound it plays is
/// sent to the Rider, whose client plays the same sound at the same place.
/// Sounds the game plays for everyone come from other steps, so none plays
/// twice. The throw hum and the Antizooka's charge follow the Shared Body's
/// state on the Rider's client instead.
/// </summary>
[HarmonyPatch]
internal static class SharedViewSounds
{
    /// <summary>The least time between two sends of one sound, as some steps play theirs each frame.</summary>
    private const float ResendSeconds = 0.1f;

    private static readonly Dictionary<string, SFX_Instance> ByName = new();

    private static readonly Dictionary<string, float> LastSent = new();

    private static float nextLookup;

    /// <summary>How deep the Carrier's client is in owner-only item steps for the Shared Body.</summary>
    private static int relaying;

    private static Action_Antizooka? chargingAntizooka;

    private static bool lentThrow;

    private static float ownThrow;

    // The game's owner-only item steps that play sounds.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Item), "FinishCastPrimary")]
    private static void RelayUseSound(Item __instance, out bool __state) => __state = Enter(__instance);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Item), "FinishCastPrimary")]
    private static void EndUseSound(bool __state) => Leave(__state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(RopeShooter), "OnPrimaryFinishedCast")]
    private static void RelayRopeShot(RopeShooter __instance, out bool __state) => __state = Enter(__instance.item);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(RopeShooter), "OnPrimaryFinishedCast")]
    private static void EndRopeShot(bool __state) => Leave(__state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(RescueHook), "Fire")]
    private static void RelayHookShot(RescueHook __instance, out bool __state) => __state = Enter(__instance.item);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(RescueHook), "Fire")]
    private static void EndHookShot(bool __state) => Leave(__state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Action_RaycastDart), "FireDart")]
    private static void RelayDartShot(Action_RaycastDart __instance, out bool __state) => __state = Enter(__instance.item);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Action_RaycastDart), "FireDart")]
    private static void EndDartShot(bool __state) => Leave(__state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Action_WarpToRandomPlayer), nameof(Action_WarpToRandomPlayer.RunAction))]
    private static void RelayWarp(Action_WarpToRandomPlayer __instance, out bool __state) => __state = Enter(__instance.item);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Action_WarpToRandomPlayer), nameof(Action_WarpToRandomPlayer.RunAction))]
    private static void EndWarp(bool __state) => Leave(__state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Candle), nameof(Candle.ToggleCandle))]
    private static void RelayCandle(Candle __instance, out bool __state) => __state = Enter(__instance.item);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Candle), nameof(Candle.ToggleCandle))]
    private static void EndCandle(bool __state) => Leave(__state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Lantern), nameof(Lantern.ToggleLantern))]
    private static void RelayLantern(Lantern __instance, out bool __state) => __state = Enter(__instance.item);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Lantern), nameof(Lantern.ToggleLantern))]
    private static void EndLantern(bool __state) => Leave(__state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ItemVFX), "Shake")]
    private static void RelayDoneSound(ItemVFX __instance, out bool __state) =>
        __state = __instance.item != null && __instance.item.finishedCast && Enter(__instance.item);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(ItemVFX), "Shake")]
    private static void EndDoneSound(bool __state) => Leave(__state);

    /// <summary>True when <paramref name="item"/> is the Shared Body's and the local player carries a Rider.</summary>
    private static bool Enter(Item? item)
    {
        Character local = Character.localCharacter;
        if (item == null
            || local == null
            || item.holderCharacter != local
            || PairRoster.RoleOf(local) != PairRole.Carrier
            || !PairRoster.TryGetPartner(local, out _))
        {
            return false;
        }

        relaying++;
        return true;
    }

    private static void Leave(bool entered)
    {
        if (entered)
        {
            relaying--;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SFX_Instance), nameof(SFX_Instance.Play), typeof(Vector3))]
    private static void SendPlayed(SFX_Instance __instance, Vector3 pos) => Send(__instance, pos);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SFX_Instance), nameof(SFX_Instance.Play), typeof(Vector3), typeof(Transform))]
    private static void SendPlayedFollowing(SFX_Instance __instance, Vector3 position) => Send(__instance, position);

    private static void Send(SFX_Instance sound, Vector3 position)
    {
        if (relaying <= 0
            || !PairRoster.TryGetPartner(Character.localCharacter, out Character rider)
            || (LastSent.TryGetValue(sound.name, out float sentAt) && Time.unscaledTime - sentAt < ResendSeconds))
        {
            return;
        }

        LastSent[sound.name] = Time.unscaledTime;

        PhotonNetwork.RaiseEvent(
            RoomKeys.ItemSoundEvent,
            new object[] { sound.name, position },
            new RaiseEventOptions { TargetActors = [PairRoster.ActorOf(rider)] },
            SendOptions.SendReliable);
    }

    internal static void Receive(EventData photonEvent)
    {
        if (photonEvent.Code != RoomKeys.ItemSoundEvent
            || photonEvent.CustomData is not object[] { Length: 2 } parts
            || parts[0] is not string name
            || parts[1] is not Vector3 position
            || RiderHud.ObservedCarrier() is not Character carrier
            || photonEvent.Sender != PairRoster.ActorOf(carrier))
        {
            return;
        }

        if (Find(name) is SFX_Instance sound)
        {
            sound.Play(position);
        }
        else
        {
            Plugin.Verbose($"No sound named {name} to play for the Rider.");
        }
    }

    private static SFX_Instance? Find(string name)
    {
        if (ByName.TryGetValue(name, out SFX_Instance sound) && sound != null)
        {
            return sound;
        }

        // Sounds are assets the held items refer to; look again at most every few seconds.
        if (Time.unscaledTime < nextLookup)
        {
            return null;
        }

        nextLookup = Time.unscaledTime + 3f;
        ByName.Clear();
        foreach (SFX_Instance instance in Resources.FindObjectsOfTypeAll<SFX_Instance>())
        {
            ByName[instance.name] = instance;
        }

        return ByName.TryGetValue(name, out sound) ? sound : null;
    }

    /// <summary>The hum of a charging throw reads the throw charge, which only the thrower's client keeps.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ItemAudioManager), "Update")]
    private static void LendThrowCharge(ItemAudioManager __instance)
    {
        Character? carrier = RiderHud.ObservedCarrier();
        if (carrier == null || __instance.character != carrier)
        {
            return;
        }

        lentThrow = true;
        ownThrow = carrier.refs.items.throwChargeLevel;
        carrier.refs.items.throwChargeLevel = SharedViewHud.RiderThrowShown;
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(ItemAudioManager), "Update")]
    private static void ReturnThrowCharge(ItemAudioManager __instance)
    {
        if (lentThrow)
        {
            lentThrow = false;
            __instance.character.refs.items.throwChargeLevel = ownThrow;
        }
    }

    /// <summary>Called every frame on the Rider's client: the Antizooka hums while the Shared Body charges it.</summary>
    internal static void Keep()
    {
        Action_Antizooka? antizooka = RiderView.TryGetCarrierOfLocalRider(out Character carrier) && carrier.data.currentItem != null
            ? carrier.data.currentItem.GetComponent<Action_Antizooka>()
            : null;
        bool charging = antizooka != null && UseFeedback.RiderSeesUse;
        if (charging && chargingAntizooka != antizooka)
        {
            StopCharge();
            chargingAntizooka = antizooka;
            antizooka!.PlayChargeFX();
        }
        else if (!charging && chargingAntizooka != null)
        {
            StopCharge();
        }
    }

    private static void StopCharge()
    {
        if (chargingAntizooka != null)
        {
            chargingAntizooka.StopChargeFX();
        }

        chargingAntizooka = null;
    }
}

/// <summary>Drives the Rider's item sounds that follow the Shared Body's state.</summary>
internal sealed class SharedViewSoundsKeeper : MonoBehaviour
{
    private void Update() => SharedViewSounds.Keep();
}
