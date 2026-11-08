using ExitGames.Client.Photon;
using HarmonyLib;
using PeakCoop.Net;
using PeakCoop.Ui;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.InputSystem;
using Zorro.Core;

namespace PeakCoop.Features;

/// <summary>
/// Keeps every Pair's carry in place for the whole Run: the Carrier's client
/// starts and restores the carry, the Rider's client keeps the Rider awake
/// and free of statuses (ADR 0002), and the game's own drops are refused.
/// </summary>
internal sealed class CarryKeeper : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const float RetrySeconds = 1f;

    private PairRole lastRole;

    private float nextCarryAttempt;

    private void Update()
    {
        if (CoopConfig.RecoveryKey.Value
            && PhotonNetwork.IsMasterClient
            && Keyboard.current != null
            && Keyboard.current.f8Key.wasPressedThisFrame)
        {
            Plugin.Log.LogInfo("Host requested Pair recovery.");
            PhotonNetwork.RaiseEvent(
                RoomKeys.RecoveryEvent,
                null,
                new RaiseEventOptions { Receivers = ReceiverGroup.All },
                SendOptions.SendReliable);
        }

        Character local = Character.localCharacter;
        PairRole role = PairRoster.RoleOf(local);
        if (role != lastRole)
        {
            Plugin.Verbose($"Local role changed {lastRole} -> {role}.");
            if (role == PairRole.Rider)
            {
                BecomeRider(local);
            }

            lastRole = role;
        }

        if (role == PairRole.Carrier && Time.unscaledTime >= nextCarryAttempt)
        {
            nextCarryAttempt = Time.unscaledTime + RetrySeconds;
            KeepCarrying(local, force: false);
        }
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != RoomKeys.RecoveryEvent)
        {
            return;
        }

        Character local = Character.localCharacter;
        switch (PairRoster.RoleOf(local))
        {
            case PairRole.Carrier:
                KeepCarrying(local, force: true);
                break;
            case PairRole.Rider:
                WakeRider(local);
                break;
        }

        CoopNotice.Info(Strings.Recovered);
    }

    private static void KeepCarrying(Character carrier, bool force)
    {
        if (!PairRoster.TryGetPartner(carrier, out Character rider))
        {
            return;
        }

        bool carrying = carrier.data.carriedPlayer == rider && rider.data.carrier == carrier;
        if (carrier.data.dead || rider.data.dead || (carrying && !force))
        {
            return;
        }

        if (carrier.data.fullyPassedOut && !force)
        {
            return;
        }

        Plugin.Verbose($"Carrier starts carrying {rider.characterName}.");
        carrier.refs.carriying.StartCarry(rider);
    }

    private static void BecomeRider(Character rider)
    {
        rider.refs.items.EquipSlot(Optionable<byte>.None);
        WakeRider(rider);
        if (PairRoster.TryGetPartner(rider, out Character carrier))
        {
            // Start looking where the Carrier looks, so pairing causes no snap.
            rider.data.lookValues = carrier.data.lookValues;
            rider.RecalculateLookDirections();
        }
    }

    private static void WakeRider(Character rider)
    {
        rider.refs.afflictions.ClearAllStatus(excludeCurse: false);
        if ((rider.data.passedOut || rider.data.fullyPassedOut) && !rider.data.dead)
        {
            Plugin.Verbose("Waking the Rider.");
            rider.photonView.RPC("ReviveCharacter", RpcTarget.All, false);
        }
    }
}

[HarmonyPatch]
internal static class CarryLockPatches
{
    private static bool IsLivePair(Character carrier, Character? carried) =>
        carried != null
        && PairRoster.TryGetPartner(carrier, out Character partner)
        && partner == carried
        && PairRoster.RoleOf(carrier) == PairRole.Carrier
        && !carrier.data.dead
        && !carried.data.dead;

    /// <summary>
    /// Clears a live Pair's carry slot for the length of one game call, so the
    /// game treats the Carrier as carrying nobody; returns what to restore.
    /// </summary>
    private static Character? HideRider(Character? carrier)
    {
        if (carrier == null || !IsLivePair(carrier, carrier.data.carriedPlayer))
        {
            return null;
        }

        Character rider = carrier.data.carriedPlayer;
        carrier.data.carriedPlayer = null;
        return rider;
    }

    private static void RestoreRider(Character? carrier, Character? rider)
    {
        if (carrier != null && rider != null)
        {
            carrier.data.carriedPlayer = rider;
        }
    }

    /// <summary>The game drops a passenger who is awake; a Rider always is.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterCarrying), "Update")]
    private static bool KeepCarryAwake(CharacterCarrying __instance) =>
        !IsLivePair(__instance.character, __instance.character.data.carriedPlayer);

    /// <summary>No drop while the Pair lives (slot 4, the DROP prompt, a backpack pickup).</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterCarrying), nameof(CharacterCarrying.Drop))]
    private static bool RefuseDrop(CharacterCarrying __instance, Character target)
    {
        if (!IsLivePair(__instance.character, target))
        {
            return true;
        }

        Plugin.Verbose("Refused a drop of a live Pair.");
        return false;
    }

    /// <summary>Carries never nest: a Carrier carries only their own Rider.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterCarrying), nameof(CharacterCarrying.StartCarry))]
    private static bool OnlyCarryOwnRider(CharacterCarrying __instance, Character target)
    {
        Character carrier = __instance.character;
        bool carrierPaired = PairRoster.RoleOf(carrier) != PairRole.None;
        bool targetPaired = PairRoster.RoleOf(target) != PairRole.None;
        if (!carrierPaired && !targetPaired)
        {
            return true;
        }

        return PairRoster.TryGetPartner(carrier, out Character partner)
            && partner == target
            && PairRoster.RoleOf(carrier) == PairRole.Carrier;
    }

    /// <summary>Nobody else picks up a member of a Pair, such as a passed-out Carrier.</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CharacterInteractible), nameof(CharacterInteractible.CanBeCarried))]
    private static void PairsAreNotCarriable(CharacterInteractible __instance, ref bool __result)
    {
        if (__result && PairRoster.RoleOf(__instance.character) != PairRole.None)
        {
            __result = false;
        }
    }

    /// <summary>
    /// A Pair's carry keeps the Carrier's back item: the game would empty the
    /// back slot onto the ground when a carry starts, as after a revival.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterCarrying), nameof(CharacterCarrying.RPCA_StartCarry))]
    private static void KeepBackItemOnPairCarry(CharacterCarrying __instance, PhotonView targetView, out BackpackSlot? __state)
    {
        __state = null;
        Character carrier = __instance.character;
        if (PairRoster.RoleOf(carrier) != PairRole.Carrier
            || !PairRoster.TryGetPartner(carrier, out Character rider)
            || rider.photonView != targetView)
        {
            return;
        }

        __state = carrier.player.backpackSlot;
        carrier.player.backpackSlot = new BackpackSlot(Player.BACKPACKSLOTINDEX);
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(CharacterCarrying), nameof(CharacterCarrying.RPCA_StartCarry))]
    private static void RestoreBackItem(CharacterCarrying __instance, BackpackSlot? __state)
    {
        if (__state != null)
        {
            __instance.character.player.backpackSlot = __state;
            __instance.character.refs.afflictions.UpdateWeight();
        }
    }

    /// <summary>
    /// With the back slot selected the game aims the interact key at the
    /// carried player, to put them down; a Pair's back slot holds a back item
    /// instead, and the interact key works on the world as usual.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Interaction), nameof(Interaction.DoInteractableRaycasts))]
    private static void InteractPastRider(out Character? __state) => __state = HideRider(Character.localCharacter);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Interaction), nameof(Interaction.DoInteractableRaycasts))]
    private static void RestoreRiderAfterInteract(Character? __state) => RestoreRider(Character.localCharacter, __state);

    /// <summary>The HUD's back slot shows the back item, not the Rider.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryItemUI), nameof(InventoryItemUI.SetItem))]
    private static void ShowBackItemInSlot(out Character? __state) => __state = HideRider(Character.observedCharacter);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(InventoryItemUI), nameof(InventoryItemUI.SetItem))]
    private static void RestoreRiderAfterSlot(Character? __state) => RestoreRider(Character.observedCharacter, __state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryItemUI), nameof(InventoryItemUI.SetSelected))]
    private static void ShowBackItemSelection(out Character? __state) => __state = HideRider(Character.observedCharacter);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(InventoryItemUI), nameof(InventoryItemUI.SetSelected))]
    private static void RestoreRiderAfterSelection(Character? __state) => RestoreRider(Character.observedCharacter, __state);

    /// <summary>
    /// Carry Weight off (the default) lifts the weight of the Rider: the game
    /// weighs the Carrier as if carrying nobody, so its own sum and cap apply.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterAfflictions), nameof(CharacterAfflictions.UpdateWeight))]
    private static void HideRiderFromWeight(CharacterAfflictions __instance, out Character? __state)
    {
        Character carrier = __instance.character;
        __state = RunRules.CarryWeight || !carrier.photonView.IsMine ? null : HideRider(carrier);
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(CharacterAfflictions), nameof(CharacterAfflictions.UpdateWeight))]
    private static void RestoreRiderAfterWeight(CharacterAfflictions __instance, Character? __state) =>
        RestoreRider(__instance.character, __state);

    /// <summary>The Shared Body has one set of statuses: the Rider gains none.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterAfflictions), nameof(CharacterAfflictions.AddStatus))]
    private static bool RiderGainsNoStatus(CharacterAfflictions __instance, ref bool __result)
    {
        if (PairRoster.RoleOf(__instance.character) != PairRole.Rider)
        {
            return true;
        }

        __result = false;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterAfflictions), nameof(CharacterAfflictions.SetStatus))]
    private static bool RiderStatusStaysClear(CharacterAfflictions __instance, float amount) =>
        amount <= 0f || PairRoster.RoleOf(__instance.character) != PairRole.Rider;
}
