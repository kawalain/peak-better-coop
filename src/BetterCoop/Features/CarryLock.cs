using System;
using ExitGames.Client.Photon;
using HarmonyLib;
using BetterCoop.Core;
using BetterCoop.Net;
using BetterCoop.Ui;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Zorro.Core;
using Zorro.Core.Serizalization;

namespace BetterCoop.Features;

/// <summary>
/// Keeps every Pair's carry in place for the whole Run: the Carrier's client
/// starts and restores the carry, the Rider's client keeps the Rider awake
/// and free of statuses (ADR 0002), and the game's own drops are refused.
/// Other carries by or of a Pair are Rescue Carries (<see cref="RescueCarry"/>).
/// </summary>
internal sealed class CarryKeeper : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const float RetrySeconds = 1f;

    private PairRole lastRole;

    private float nextCarryAttempt;

    private float nextHandCheck;

    private void Update()
    {
        try
        {
            Tick();
        }
        catch (Exception exception)
        {
            Guard.Report("CarryKeeper.Update", exception);
        }
    }

    /// <summary>The Troop Leader asks every client to re-attach its Pair and wake its Rider.</summary>
    internal static bool RequestRecovery()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            return false;
        }

        Plugin.Log.LogInfo("Troop Leader requested Pair recovery.");
        PhotonNetwork.RaiseEvent(
            RoomKeys.RecoveryEvent,
            null,
            new RaiseEventOptions { Receivers = ReceiverGroup.All },
            SendOptions.SendReliable);
        return true;
    }

    private void Tick()
    {
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

        SharedLife.Keep();
        RescueCarry.Keep();
        if (role == PairRole.Rider && Time.unscaledTime >= nextHandCheck)
        {
            nextHandCheck = Time.unscaledTime + 0.5f;
            EmptyRiderHandsAndBack(local);
        }
    }

    /// <summary>
    /// The Rider's hands and back stay empty (ADR 0002): an item that lands
    /// there, such as one a cheat spawns into the hand or a back item worn
    /// before pairing, is dropped in front of the Shared Body.
    /// </summary>
    private static void EmptyRiderHandsAndBack(Character rider)
    {
        if (rider.data.dead || !PairRoster.TryGetPartner(rider, out Character carrier))
        {
            return;
        }

        Vector3 forward = Vector3.ProjectOnPlane(carrier.data.lookDirection, Vector3.up).normalized;
        Vector3 at = carrier.Center + (forward * 0.8f);
        CharacterItems items = rider.refs.items;
        if (rider.data.currentItem != null)
        {
            ItemSlot? slot = items.currentSelectedSlot.IsSome ? rider.player.GetItemSlot(items.currentSelectedSlot.Value) : null;
            if (slot != null && !slot.IsEmpty())
            {
                Plugin.Verbose($"Dropping {slot.GetPrefabName()} from the Rider's hand.");
                items.photonView.RPC(
                    "DropItemRpc",
                    RpcTarget.All,
                    0f,
                    items.currentSelectedSlot.Value,
                    at,
                    Vector3.zero,
                    Quaternion.identity,
                    slot.data,
                    true);
                CoopNotice.Show(NoticeKind.Tag, Strings.RiderHandEmptied);
            }
            else
            {
                items.EquipSlot(Optionable<byte>.None);
            }
        }

        // A Pair Shuffle or Role Swap is moving inventories; judge the back once it lands.
        if (rider.player.backpackSlot.IsEmpty() || PairShuffle.LocalLocked)
        {
            return;
        }

        if (carrier.player.backpackSlot.IsEmpty())
        {
            Plugin.Verbose($"Handing {rider.player.backpackSlot.GetPrefabName()} from the Rider's back to the Carrier's.");
            HandBackItemToCarrier(rider.player, carrier.player);
            return;
        }

        Plugin.Verbose($"Dropping {rider.player.backpackSlot.GetPrefabName()} from the Rider's back.");
        items.photonView.RPC("DropItemFromSlotRPC", RpcTarget.All, (byte)Player.BACKPACKSLOTINDEX, at + (Vector3.up * 0.5f));
        CoopNotice.Show(NoticeKind.Tag, Strings.RiderBackEmptied);
    }

    /// <summary>
    /// Moves the Rider's back item onto the Carrier's empty back, the way a
    /// Handover moves inventories: a forced sync of both players to every
    /// client. Whatever put it there (a revival, another mod's carry), the
    /// Shared Body keeps it and nothing lands on the ground.
    /// </summary>
    private static void HandBackItemToCarrier(Player rider, Player carrier)
    {
        var emptyBack = new BackpackSlot(Player.BACKPACKSLOTINDEX);
        byte[] riderData = IBinarySerializable.ToManagedArray(new InventorySyncData(rider.itemSlots, emptyBack, rider.tempFullSlot));
        byte[] carrierData = IBinarySerializable.ToManagedArray(new InventorySyncData(carrier.itemSlots, rider.backpackSlot, carrier.tempFullSlot));
        carrier.photonView.RPC("SyncInventoryRPC", RpcTarget.All, carrierData, true);
        rider.photonView.RPC("SyncInventoryRPC", RpcTarget.All, riderData, true);
    }

    public void OnEvent(EventData photonEvent)
    {
        try
        {
            HandleEvent(photonEvent);
            TroopLeaderRevive.Handle(photonEvent);
            RescueCarry.Handle(photonEvent);
        }
        catch (Exception exception)
        {
            Guard.Report("CarryKeeper.OnEvent", exception);
        }
    }

    private static void HandleEvent(EventData photonEvent)
    {
        // Only the Troop Leader repairs Pairs; another sender, or another mod
        // that reuses the event code, is ignored.
        if (photonEvent.Code != RoomKeys.RecoveryEvent
            || PhotonNetwork.MasterClient == null
            || photonEvent.Sender != PhotonNetwork.MasterClient.ActorNumber)
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

        CoopNotice.Show(NoticeKind.Tag, Strings.Recovered);
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

        // After a Role Swap or Pair Shuffle the former Carrier drops this one
        // first, and a rescuer puts down a Rescued who gained this Pair.
        if ((carrier.data.carrier != null || (rider.data.carrier != null && rider.data.carrier != carrier)) && !force)
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

    internal static void WakeRider(Character rider)
    {
        rider.refs.afflictions.ClearAllStatus(excludeCurse: false, excludePetrify: false);
        if ((rider.data.passedOut || rider.data.fullyPassedOut) && !rider.data.dead)
        {
            Plugin.Verbose("Waking the Rider.");
            TroopLeaderRevive.Wake(rider);
        }
    }
}

[HarmonyPatch]
[HarmonyWrapSafe]
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

    /// <summary>
    /// The game's warp turns a body's colliders back on as it ends, even a
    /// Rider's still being carried, and a physics step later the two bodies
    /// push each other apart. A carried Rider's colliders stay off while it
    /// warps; dropping it still turns them on, as the carry has ended by then.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterRagdoll), nameof(CharacterRagdoll.ToggleCollision))]
    private static bool KeepWarpedRiderClear(CharacterRagdoll __instance, bool enableCollision)
    {
        Character body = __instance.character;
        return !(enableCollision
            && body != null
            && body.warping
            && body.data.isCarried
            && IsLivePair(body.data.carrier, body));
    }

    private const float DropNoteSeconds = 10f;

    private static float nextDropNote;

    private static int unnotedDrops;

    /// <summary>
    /// No drop while the Pair lives (slot 4, the DROP prompt, a backpack
    /// pickup). Another mod may ask every frame, so the log notes the
    /// refusals at most once per <see cref="DropNoteSeconds"/>.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterCarrying), nameof(CharacterCarrying.Drop))]
    private static bool RefuseDrop(CharacterCarrying __instance, Character target)
    {
        if (!IsLivePair(__instance.character, target))
        {
            return true;
        }

        if (RescueCarry.PutDownForBackSlot(__instance.character))
        {
            return false;
        }

        if (Time.unscaledTime < nextDropNote)
        {
            unnotedDrops++;
            return false;
        }

        Plugin.Verbose(unnotedDrops > 0
            ? $"Refused a drop of a live Pair ({unnotedDrops} more since the last note)."
            : "Refused a drop of a live Pair.");
        unnotedDrops = 0;
        nextDropNote = Time.unscaledTime + DropNoteSeconds;
        return false;
    }

    /// <summary>
    /// A carried player whose Carrier left the room stays marked as carried:
    /// the game only turns their body's physics back on. Marked, they take no
    /// statuses, and dropping them would reach for a Carrier who is gone. The
    /// mark goes with the Carrier, on every client.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CharacterCarrying), nameof(CharacterCarrying.CarrierGone))]
    private static void ForgetGoneCarrier(CharacterCarrying __instance)
    {
        Character character = __instance.character;
        if (character != null && character.data.isCarried && character.data.carrier == null)
        {
            character.data.isCarried = false;
            Plugin.Verbose($"Scout {PairRoster.ActorOf(character)} was carried by someone who left; no longer carried.");
        }
    }

    /// <summary>
    /// An Unmodded Player's game lets them pick up a passed-out member of a
    /// Pair, which the mod never judges on their client, and cannot keep the
    /// Pair's carry while it does (ADR 0010): the Troop Leader sets the
    /// member down again at once.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CharacterCarrying), nameof(CharacterCarrying.RPCA_StartCarry))]
    private static void SetDownUnmoddedCarry(CharacterCarrying __instance, PhotonView targetView)
    {
        if (!PhotonNetwork.IsMasterClient || targetView == null)
        {
            return;
        }

        Character carrier = __instance.character;
        Character target = targetView.GetComponent<Character>();
        if (carrier == null
            || target == null
            || carrier.data.carriedPlayer != target
            || carrier.photonView.Owner is not Photon.Realtime.Player owner
            || owner.IsLocal
            || RoomProtocol.Speaks(owner)
            || PairRoster.RoleOf(target) == PairRole.None)
        {
            return;
        }

        Plugin.Log.LogInfo($"Unmodded Player {owner.ActorNumber} picked up Pair member {PairRoster.ActorOf(target)}; set down.");
        carrier.refs.carriying.Drop(target);
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

    /// <summary>
    /// The HUD's back slot shows the back item, not the Rider; during a
    /// Rescue Carry it shows the Rescued, as the game's does for a carry.
    /// </summary>
    private static Character? ShowRescuedOrBackItem(Character? carrier)
    {
        Character? rider = HideRider(carrier);
        if (rider != null && RescueCarry.RescuedOn(carrier!) is Character rescued)
        {
            carrier!.data.carriedPlayer = rescued;
        }

        return rider;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryItemUI), nameof(InventoryItemUI.SetItem))]
    private static void ShowBackItemInSlot(out Character? __state) => __state = ShowRescuedOrBackItem(Character.observedCharacter);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(InventoryItemUI), nameof(InventoryItemUI.SetItem))]
    private static void RestoreRiderAfterSlot(Character? __state) => RestoreRider(Character.observedCharacter, __state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryItemUI), nameof(InventoryItemUI.SetSelected))]
    private static void ShowBackItemSelection(out Character? __state) => __state = ShowRescuedOrBackItem(Character.observedCharacter);

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
