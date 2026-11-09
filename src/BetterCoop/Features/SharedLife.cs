using System.Collections.Generic;
using ExitGames.Client.Photon;
using HarmonyLib;
using BetterCoop.Core;
using BetterCoop.Net;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace BetterCoop.Features;

/// <summary>
/// Shared Life: the Rider lives and dies with the Shared Body. The Rider's
/// client follows the Carrier's state (<see cref="SharedLifeRules"/>); the
/// patches below only make the common cases immediate.
/// </summary>
[HarmonyPatch]
[HarmonyWrapSafe]
internal static class SharedLife
{
    private const float RetrySeconds = 1f;

    private static float riderRevivedAt = float.NegativeInfinity;

    private static float nextAttempt;

    /// <summary>
    /// The scouts whose leave to be eaten this client has not been told: a
    /// join sync sent while the scout was a skeleton says no whatever their
    /// setting, and only their own consent says it again.
    /// </summary>
    private static readonly HashSet<int> consentUnknown = new();

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
                TroopLeaderRevive.AtPosition(rider, carrier.Center + Vector3.up, applyStatus: false, statueSegment: -1);
                break;
            case RiderLifeAction.Wake:
                CarryKeeper.WakeRider(rider);
                break;
        }
    }

    /// <summary>
    /// The Rider dies when the Carrier dies, at once rather than at the next
    /// check. Every death the game lets through, sent or local, ends here; a
    /// kill the game refused does not.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Character), nameof(Character.GoDeadMode))]
    private static void RiderDiesWithCarrier(Character __instance)
    {
        if (__instance.data.dead
            && PairRoster.RoleOf(__instance) == PairRole.Carrier
            && PairRoster.TryGetPartner(__instance, out Character rider)
            && rider.IsLocal
            && !rider.data.dead)
        {
            Plugin.Verbose($"{__instance.characterName} died; their Rider dies with them.");
            rider.DieInstantly();
        }
    }

    /// <summary>
    /// On the Rider's client, a death of their Carrier counts only from those
    /// the game lets kill a scout: the Carrier, the Troop Leader, this client,
    /// or a cannibal both of them allow. Any other client could make the
    /// Rider die while the Carrier lives, as the game checks the sender only
    /// on the dying scout's own client.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Character), nameof(Character.RPCA_Die))]
    private static bool CarrierDiesOnlyByRightfulKill(Character __instance, PhotonMessageInfo info) =>
        !IsForgedForRider(__instance, info.Sender, "death", canEat: true);

    /// <summary>
    /// A join sync of the Rider's Carrier counts only from the Carrier or the
    /// Troop Leader, who alone send it; another client's could mark the
    /// Carrier dead on the Rider's client, and the Rider would die to match.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterData), nameof(CharacterData.RPC_SyncOnJoin))]
    private static bool CarrierSyncsOnlyFromRightfulSender(CharacterData __instance) =>
        !IsForgedForRider(__instance.character, RpcSender.Current, "join sync", canEat: false);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CharacterData), nameof(CharacterData.RPC_SyncOnJoin))]
    private static void NoteConsentFromSync(CharacterData __instance, bool cannibalismPermitted, bool isSkeleton, bool __runOriginal)
    {
        if (!__runOriginal)
        {
            return;
        }

        int view = __instance.photonView.ViewID;
        if (isSkeleton && !cannibalismPermitted)
        {
            consentUnknown.Add(view);
        }
        else
        {
            consentUnknown.Remove(view);
        }
    }

    /// <summary>
    /// The Carrier's leave to be eaten counts only from the Carrier or the
    /// Troop Leader; another client's could open the cannibal's way above.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterData), nameof(CharacterData.RPCA_SyncCanBeCannibalized))]
    private static bool CarrierConsentsOnlyForThemselves(CharacterData __instance) =>
        !IsForgedForRider(__instance.character, RpcSender.Current, "cannibalism consent", canEat: false);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CharacterData), nameof(CharacterData.RPCA_SyncCanBeCannibalized))]
    private static void NoteConsent(CharacterData __instance, bool __runOriginal)
    {
        if (__runOriginal)
        {
            consentUnknown.Remove(__instance.photonView.ViewID);
        }
    }

    private static bool IsForgedForRider(Character target, Photon.Realtime.Player? sender, string what, bool canEat)
    {
        Character rider = Character.localCharacter;
        if (sender == null
            || target == null
            || rider == null
            || target == rider
            || PairRoster.RoleOf(rider) != PairRole.Rider
            || !PairRoster.TryGetPartner(rider, out Character carrier)
            || carrier != target
            || sender.IsLocal
            || sender.IsMasterClient
            || sender.ActorNumber == target.photonView.Owner?.ActorNumber
            || (canEat && CouldEat(sender, target)))
        {
            return false;
        }

        Plugin.Log.LogWarning($"Refused a {what} of Carrier {target.characterName} from player {sender.ActorNumber}, who may not send it.");
        return true;
    }

    /// <summary>
    /// The game's <c>Character.CouldBeEatenBy</c> as the Carrier's own client
    /// answers it: the Carrier's setting allows being eaten (shared as their
    /// raw consent) and the eater allows cannibalism. The Rider's own setting
    /// has no say, or a Carrier eaten rightfully would leave the Rider alive.
    /// A skeleton shares no consent, so while it is one, or until it says
    /// again after a join sync sent while it was one, its setting is taken to
    /// allow it.
    /// </summary>
    private static bool CouldEat(Photon.Realtime.Player sender, Character carrier) =>
        (carrier.data._cannibalismPermitted || carrier.data.isSkeleton || consentUnknown.Contains(carrier.photonView.ViewID))
        && PairRoster.TryGetCharacter(sender.ActorNumber, out Character eater)
        && eater.data.cannibalismPermitted;

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
    /// The Troop Leader sends the partner's revival (ADR 0007).
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Character), nameof(Character.RPCA_ReviveAtPosition))]
    private static void PartnerRevivesToo(Character __instance, Vector3 position, bool applyStatus, int statueSegment)
    {
        if (PairRoster.RoleOf(__instance) == PairRole.Rider)
        {
            if (__instance.IsLocal)
            {
                riderRevivedAt = Time.time;
            }

            ClearRemains(__instance);
        }

        if (!PhotonNetwork.IsMasterClient
            || !PairRoster.TryGetPartner(__instance, out Character partner)
            || !partner.data.dead)
        {
            return;
        }

        Plugin.Verbose($"{__instance.characterName} was revived; their partner revives with them.");
        partner.photonView.RPC("RPCA_ReviveAtPosition", RpcTarget.All, position, applyStatus, statueSegment);
    }

    /// <summary>
    /// A Rider who died is back on their Carrier's back, so the remains the
    /// game left where they died would sit on that back, in front of the
    /// Pair's view. The game clears remains only for a scout brought back
    /// as a skeleton.
    /// </summary>
    private static void ClearRemains(Character rider)
    {
        foreach (Skelleton remains in UnityEngine.Object.FindObjectsByType<Skelleton>(FindObjectsSortMode.None))
        {
            if (remains.spawnedFromCharacter == rider)
            {
                UnityEngine.Object.Destroy(remains.gameObject);
                Plugin.Verbose($"Cleared the remains of Rider {rider.characterName}, revived on their Carrier.");
            }
        }
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

/// <summary>
/// Revives of Pair members are sent by the Troop Leader, as the game's own
/// revives are (ADR 0007): a member's client asks, and the Troop Leader sends
/// the revive. A lost request is asked again by the caller's next check.
/// </summary>
internal static class TroopLeaderRevive
{
    /// <summary>Brings a dead Pair member back at <paramref name="position"/>.</summary>
    internal static void AtPosition(Character target, Vector3 position, bool applyStatus, int statueSegment) =>
        Request(target, atPosition: true, position, applyStatus, statueSegment);

    /// <summary>Wakes a passed-out Pair member where they are.</summary>
    internal static void Wake(Character target) =>
        Request(target, atPosition: false, Vector3.zero, applyStatus: false, statueSegment: -1);

    private static void Request(Character target, bool atPosition, Vector3 position, bool applyStatus, int statueSegment)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            Send(target, atPosition, position, applyStatus, statueSegment);
            return;
        }

        PhotonNetwork.RaiseEvent(
            RoomKeys.ReviveRequestEvent,
            new object[] { target.photonView.ViewID, atPosition, position, applyStatus, statueSegment },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
            SendOptions.SendReliable);
    }

    /// <summary>Carries out a member's request, on the Troop Leader's client.</summary>
    /// <summary>How soon the Troop Leader relays another revive of the same scout.</summary>
    private const float RelaySeconds = 1.5f;

    /// <summary>How far from their Carrier a Rider may ask to come back, allowing for the Troop Leader's view lagging behind.</summary>
    private const float NearCarrier = 8f;

    /// <summary>When the Troop Leader last relayed a revive, by the target's view.</summary>
    private static readonly System.Collections.Generic.Dictionary<int, float> Relayed = new();

    private static bool NearOwnCarrier(Character target, Pair pair, Vector3 position) =>
        PairRoster.ActorOf(target) == pair.Rider
        && PairRoster.TryGetCharacter(pair.Carrier, out Character carrier)
        && WireLimits.IsFinite(position.x) && WireLimits.IsFinite(position.y) && WireLimits.IsFinite(position.z)
        && (position - carrier.Center).sqrMagnitude <= NearCarrier * NearCarrier;

    internal static void Handle(EventData photonEvent)
    {
        if (photonEvent.Code != RoomKeys.ReviveRequestEvent
            || !PhotonNetwork.IsMasterClient
            || photonEvent.CustomData is not object[] { Length: 5 } parts
            || parts[0] is not int viewId
            || parts[1] is not bool atPosition
            || parts[2] is not Vector3 position
            || parts[3] is not bool applyStatus
            || parts[4] is not int statueSegment)
        {
            return;
        }

        // Only a member of the target's own Pair may ask, and only as the mod
        // asks: a Rider back at their Carrier, or a member woken where they
        // are; never a statue's revive or its statuses.
        PhotonView view = PhotonView.Find(viewId);
        Character? target = view != null ? view.GetComponent<Character>() : null;
        if (target == null
            || !PairRoster.TryGetPair(target, out Pair pair)
            || !pair.Contains(photonEvent.Sender)
            || applyStatus
            || statueSegment != -1
            || (atPosition && !NearOwnCarrier(target, pair, position)))
        {
            Plugin.Verbose($"Ignored a revive request from #{photonEvent.Sender} for view {viewId}.");
            return;
        }

        // A request that crossed a revive already sent (the member's next
        // check asks again if this client's view of the target lags).
        bool needed = atPosition
            ? target.data.dead
            : !target.data.dead && (target.data.passedOut || target.data.fullyPassedOut);
        // Each revive the Troop Leader sends reaches every client: one per
        // target every so often, however often a member asks.
        float now = Time.unscaledTime;
        if (!needed
            || (Relayed.TryGetValue(viewId, out float last) && now - last < RelaySeconds)
            || !WireLimits.IsFinite(position.x) || !WireLimits.IsFinite(position.y) || !WireLimits.IsFinite(position.z))
        {
            return;
        }

        if (Relayed.Count > 64)
        {
            Relayed.Clear();
        }

        Relayed[viewId] = now;
        Send(target, atPosition, position, applyStatus, statueSegment);
    }

    private static void Send(Character target, bool atPosition, Vector3 position, bool applyStatus, int statueSegment)
    {
        Plugin.Verbose($"Troop Leader {(atPosition ? "revives" : "wakes")} {target.characterName}.");
        if (atPosition)
        {
            target.photonView.RPC("RPCA_ReviveAtPosition", RpcTarget.All, position, applyStatus, statueSegment);
        }
        else
        {
            target.photonView.RPC("ReviveCharacter", RpcTarget.All, applyStatus);
        }
    }
}
