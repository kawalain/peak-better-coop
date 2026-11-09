using ExitGames.Client.Photon;
using HarmonyLib;
using BetterCoop.Core;
using BetterCoop.Net;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Zorro.Core;

namespace BetterCoop.Features;

/// <summary>
/// Rescue Carry (ADR 0010): a Shared Body carries a passed-out scout on its
/// Rider's back, and an Unpaired Player carries a passed-out Shared Body with
/// its Rider. Every carry is the game's own. For a Shared Body the Carrier's
/// client asks, as the interact key is the Carrier's, and the Rider's
/// client, which owns the Rider's carry, picks the Rescued up and puts them
/// down; the rescuer's client puts the Rescued down when the rescue can no
/// longer hold.
/// </summary>
[HarmonyPatch]
[HarmonyWrapSafe]
internal static class RescueCarry
{
    /// <summary>How far from the Shared Body a Rider picks up a Rescued, allowing for lag.</summary>
    private const float Reach = 8f;

    private const float RetrySeconds = 0.5f;

    /// <summary>The rescue this client carries, by the Rescued's view, as it began.</summary>
    private static (int View, RescueHold Began)? held;

    private static float nextPutDown;

    /// <summary>Set while this client's Rider starts a rescue the Carrier asked for.</summary>
    private static bool startingRescue;

    /// <summary>Set while the local Carrier presses the back-slot key or takes a back item.</summary>
    private static bool backSlotTaken;

    /// <summary>The Shared Body weighed now, when it carries a Rescued.</summary>
    private static Character? weighing;

    /// <summary>The Rescued a Shared Body carries on its Rider's back, if any.</summary>
    internal static Character? RescuedOn(Character carrier) =>
        PairRoster.RoleOf(carrier) == PairRole.Carrier
        && PairRoster.TryGetPartner(carrier, out Character rider)
        && rider.data.carriedPlayer is Character rescued
        && rescued != null
        && rescued != carrier
            ? rescued
            : null;

    private static RescueStanding StandingOf(Character character) => PairRoster.RoleOf(character) switch
    {
        PairRole.Carrier => RescueStanding.Carrier,
        PairRole.Rider => RescueStanding.Rider,
        _ => RescueStanding.Unpaired,
    };

    private static (int Carrier, int Rider)? PairKeyOf(Character character) =>
        PairRoster.TryGetPair(character, out Pair pair) ? (pair.Carrier, pair.Rider) : null;

    private static bool Rescuable(Character target) =>
        !target.isBot
        && RescueRules.CanBeRescued(target.data.fullyPassedOut, target.data.dead, target.data.carrier != null, StandingOf(target));

    private static bool Up(Character body) => !body.data.dead && !body.data.passedOut && !body.data.fullyPassedOut;

    /// <summary>A Shared Body awake, alive, and with no Rescued on its Rider yet.</summary>
    private static bool FreeToRescue(Character carrier) =>
        Up(carrier)
        && PairRoster.TryGetPartner(carrier, out Character rider)
        && rider.data.carriedPlayer == null;

    /// <summary>
    /// The game offers to carry only a passed-out scout; a Pair's member is
    /// offered only as a Rescued and a Pair carries only as a rescuer, both
    /// while the Run Rules allow it.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CharacterInteractible), nameof(CharacterInteractible.CanBeCarried))]
    private static void OnlyRescuesCarryPairs(CharacterInteractible __instance, ref bool __result)
    {
        Character target = __instance.character;
        Character local = Character.localCharacter;
        PairRole localRole = PairRoster.RoleOf(local);
        if (!__result || (localRole == PairRole.None && PairRoster.RoleOf(target) == PairRole.None))
        {
            return;
        }

        __result = RunRules.RescueCarry
            && localRole != PairRole.Rider
            && Rescuable(target)
            && (localRole != PairRole.Carrier || FreeToRescue(local));
    }

    /// <summary>
    /// A Carrier carries their own Rider, and anyone else only in a Rescue
    /// Carry: a Carrier's pickup asks the Rider to carry the Rescued, a
    /// Rider carries only what the Carrier asked for, and an Unpaired Player
    /// carries a passed-out Shared Body as the game does.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterCarrying), nameof(CharacterCarrying.StartCarry))]
    private static bool StartOnlyPairOrRescue(CharacterCarrying __instance, Character target)
    {
        Character carrier = __instance.character;
        PairRole role = PairRoster.RoleOf(carrier);
        if (role == PairRole.None && PairRoster.RoleOf(target) == PairRole.None)
        {
            return true;
        }

        if (role == PairRole.Carrier && PairRoster.TryGetPartner(carrier, out Character partner) && partner == target)
        {
            return true;
        }

        if (!RunRules.RescueCarry || !Rescuable(target))
        {
            return false;
        }

        switch (role)
        {
            case PairRole.None:
                return true;
            case PairRole.Rider:
                return startingRescue;
            default:
                if (carrier.IsLocal && FreeToRescue(carrier))
                {
                    // As the game's carry does, the hands empty as the rescue starts.
                    carrier.refs.items.EquipSlot(Optionable<byte>.None);
                    AskRider(carrier, target, pickUp: true);
                }

                return false;
        }
    }

    private static void AskRider(Character carrier, Character target, bool pickUp)
    {
        if (!PairRoster.TryGetPartner(carrier, out Character rider))
        {
            return;
        }

        Plugin.Verbose($"Asking the Rider to {(pickUp ? "pick up" : "put down")} {target.characterName}.");
        PhotonNetwork.RaiseEvent(
            RoomKeys.RescueEvent,
            new object[] { target.photonView.ViewID, pickUp },
            new RaiseEventOptions { TargetActors = [PairRoster.ActorOf(rider)] },
            SendOptions.SendReliable);
    }

    /// <summary>The Rider's client carries out its Carrier's request.</summary>
    internal static void Handle(EventData photonEvent)
    {
        if (photonEvent.Code != RoomKeys.RescueEvent
            || photonEvent.CustomData is not object[] { Length: 2 } parts
            || parts[0] is not int viewId
            || parts[1] is not bool pickUp)
        {
            return;
        }

        Character rider = Character.localCharacter;
        PhotonView view = PhotonView.Find(viewId);
        Character? target = view != null ? view.GetComponent<Character>() : null;
        if (rider == null
            || target == null
            || PairRoster.RoleOf(rider) != PairRole.Rider
            || !PairRoster.TryGetPartner(rider, out Character carrier)
            || PairRoster.ActorOf(carrier) != photonEvent.Sender)
        {
            Plugin.Verbose($"Ignored a Rescue Carry request from #{photonEvent.Sender} for view {viewId}.");
            return;
        }

        if (!pickUp)
        {
            if (rider.data.carriedPlayer == target)
            {
                Plugin.Verbose($"Putting down {target.characterName}, as the Carrier asked.");
                rider.refs.carriying.Drop(target);
            }

            return;
        }

        if (!RunRules.RescueCarry
            || rider.data.carriedPlayer != null
            || target == rider
            || target == carrier
            || !Up(carrier)
            || !Rescuable(target)
            || (target.Center - carrier.Center).sqrMagnitude > Reach * Reach)
        {
            Plugin.Verbose($"Did not pick up {target.characterName}: the rescue no longer fits.");
            return;
        }

        Plugin.Verbose($"Picking up {target.characterName} on the Rider's back.");
        startingRescue = true;
        try
        {
            rider.refs.carriying.StartCarry(target);
        }
        finally
        {
            startingRescue = false;
        }
    }

    /// <summary>
    /// Notes a rescue this client carries; the Shared Body drops its back
    /// item where the Rescued lay, as the game's carrier does.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CharacterCarrying), nameof(CharacterCarrying.RPCA_StartCarry))]
    private static void NoteRescue(CharacterCarrying __instance, PhotonView targetView)
    {
        Character rescuer = __instance.character;
        Character? rescued = targetView != null ? targetView.GetComponent<Character>() : null;
        if (rescuer == null
            || rescued == null
            || rescuer.data.carriedPlayer != rescued
            || (PairRoster.RoleOf(rescuer) == PairRole.None && PairRoster.RoleOf(rescued) == PairRole.None)
            || (PairRoster.TryGetPartner(rescuer, out Character partner) && partner == rescued))
        {
            return;
        }

        if (rescuer.IsLocal)
        {
            held = (targetView!.ViewID, new RescueHold(PairKeyOf(rescuer), PairKeyOf(rescued)));
            Plugin.Verbose($"Rescue Carry of {rescued.characterName} began.");
        }

        if (PairRoster.RoleOf(rescuer) == PairRole.Rider
            && PairRoster.TryGetPartner(rescuer, out Character carrier)
            && carrier.IsLocal
            && !carrier.player.backpackSlot.IsEmpty())
        {
            Plugin.Verbose($"Dropping {carrier.player.backpackSlot.GetPrefabName()} from the Shared Body's back for the rescue.");
            carrier.refs.items.photonView.RPC("DropItemFromSlotRPC", RpcTarget.All, (byte)Player.BACKPACKSLOTINDEX, rescued.Center);
        }
    }

    /// <summary>
    /// Called every frame on every client: puts the Rescued down when the
    /// rescue this client carries can no longer hold (ADR 0010).
    /// </summary>
    internal static void Keep()
    {
        if (held is not (int view, RescueHold began))
        {
            return;
        }

        Character? local = Character.localCharacter;
        Character? rescued = local != null ? local.data.carriedPlayer : null;
        if (local == null || rescued == null || rescued.photonView.ViewID != view)
        {
            held = null;
            return;
        }

        bool paired = PairRoster.TryGetPartner(local, out Character partner);
        bool sharedBodyUp = PairRoster.RoleOf(local) != PairRole.Rider || (paired && Up(partner));
        bool changing = PairShuffle.Locked(PairRoster.ActorOf(local))
            || PairShuffle.Locked(PairRoster.ActorOf(rescued))
            || (paired && PairShuffle.Locked(PairRoster.ActorOf(partner)));
        var now = new RescueHold(PairKeyOf(local), PairKeyOf(rescued));
        if (RescueRules.Keeps(began, now, RunRules.RescueCarry, sharedBodyUp, changing) || Time.unscaledTime < nextPutDown)
        {
            return;
        }

        nextPutDown = Time.unscaledTime + RetrySeconds;
        Plugin.Verbose($"Putting down {rescued.characterName}: the rescue can no longer hold.");
        local.refs.carriying.Drop(rescued);
    }

    /// <summary>
    /// The game puts a carried scout down when the back-slot key is pressed
    /// or a back item is taken; for a Shared Body that reaches the Rider's
    /// carry, which the mod refuses to drop, so it puts down the Rescued.
    /// </summary>
    internal static bool PutDownForBackSlot(Character carrier)
    {
        if (!backSlotTaken || !carrier.IsLocal || RescuedOn(carrier) is not Character rescued)
        {
            return false;
        }

        AskRider(carrier, rescued, pickUp: false);
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterItems), "DoSwitching")]
    private static void SwitchingSlots(CharacterItems __instance) => backSlotTaken = __instance.character.IsLocal;

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(CharacterItems), "DoSwitching")]
    private static void SwitchedSlots() => backSlotTaken = false;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterItems), nameof(CharacterItems.OnPickupAccepted))]
    private static void TakingItem(CharacterItems __instance, byte slotID) =>
        backSlotTaken = __instance.character.IsLocal && slotID == Player.BACKPACKSLOTINDEX;

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(CharacterItems), nameof(CharacterItems.OnPickupAccepted))]
    private static void TookItem() => backSlotTaken = false;

    /// <summary>
    /// The game puts a carry's weight on its carrier, here the Rider, whose
    /// statuses stay clear: the Shared Body bears the Rescued's weight, always
    /// as in unmodded PEAK; Carry Weight governs only its own Rider.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterAfflictions), nameof(CharacterAfflictions.UpdateWeight))]
    private static void WeighRescued(CharacterAfflictions __instance)
    {
        Character body = __instance.character;
        weighing = body.photonView.IsMine && RescuedOn(body) != null ? body : null;
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(CharacterAfflictions), nameof(CharacterAfflictions.UpdateWeight))]
    private static void WeighedRescued() => weighing = null;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterAfflictions), nameof(CharacterAfflictions.SetStatus))]
    private static void AddRescuedWeight(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, ref float amount)
    {
        if (weighing != null && weighing == __instance.character && statusType == CharacterAfflictions.STATUSTYPE.Weight)
        {
            amount += RescueRules.RescuedWeight;
        }
    }
}
