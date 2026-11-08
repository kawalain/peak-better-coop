using ExitGames.Client.Photon;
using HarmonyLib;
using PeakCoop.Core;
using PeakCoop.Net;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace PeakCoop.Features;

/// <summary>
/// The Control Split (ADR 0002): the Rider's look, use, secondary use,
/// scroll, and ping drive the Shared Body; every other action is the
/// Carrier's. The Rider's client samples those actions and sends them to the
/// Carrier, whose client plays them into the Shared Body.
/// </summary>
[HarmonyPatch]
internal static class ControlSplit
{
    private const float SendInterval = 1f / 30f;

    // Rider side: what the Rider produced so far.
    private static ButtonCounters primaryOut;
    private static ButtonCounters secondaryOut;
    private static ButtonCounters scrollForwardOut;
    private static ButtonCounters scrollBackwardOut;
    private static float scrollTotalOut;
    private static int sequenceOut;
    private static RiderActions? lastSent;
    private static float nextSend;

    // Carrier side: what the Rider sent, replayed frame by frame.
    private static readonly RelayedButton PrimaryIn = new();
    private static readonly RelayedButton SecondaryIn = new();
    private static readonly LookFollower LookIn = new();
    private static ButtonCounters scrollForwardIn;
    private static ButtonCounters scrollBackwardIn;
    private static ushort scrollForwardApplied;
    private static ushort scrollBackwardApplied;
    private static float scrollTotalIn;
    private static float scrollTotalApplied;
    private static bool receivedAny;
    private static int lastSequenceIn = int.MinValue;
    private static int lookFrame = -1;

    private static int partnerActor = -1;

    /// <summary>The local player's live Pair: both members present and alive.</summary>
    private static bool TryGetLivePair(out PairRole role, out Character partner)
    {
        Character local = Character.localCharacter;
        role = PairRoster.RoleOf(local);
        partner = null!;
        if (role == PairRole.None
            || !PairRoster.TryGetPartner(local, out partner)
            || local.data.dead
            || partner.data.dead)
        {
            Forget();
            return false;
        }

        int actor = PairRoster.ActorOf(partner);
        if (actor != partnerActor)
        {
            Forget();
            partnerActor = actor;
            Plugin.Verbose($"Control Split active as {role} with actor {actor}.");
        }

        return true;
    }

    private static void Forget()
    {
        if (partnerActor == -1)
        {
            return;
        }

        partnerActor = -1;
        lastSent = null;
        PrimaryIn.Reset();
        SecondaryIn.Reset();
        LookIn.Reset();
        receivedAny = false;
        lastSequenceIn = int.MinValue;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CharacterInput), nameof(CharacterInput.Sample))]
    private static void SplitSampledInput(CharacterInput __instance, bool playerMovementActive)
    {
        Character local = Character.localCharacter;
        if (local == null || local.input != __instance || !TryGetLivePair(out PairRole role, out _))
        {
            return;
        }

        if (role == PairRole.Rider)
        {
            TakeRiderActions(__instance);
        }
        else
        {
            PlayRiderActions(__instance, playerMovementActive);
        }
    }

    /// <summary>The Rider keeps look, ping, pause, and push-to-talk; the rest is sent or dropped.</summary>
    private static void TakeRiderActions(CharacterInput input)
    {
        primaryOut = primaryOut.After(input.usePrimaryWasPressed, input.usePrimaryIsPressed, input.usePrimaryWasReleased);
        secondaryOut = secondaryOut.After(input.useSecondaryWasPressed, input.useSecondaryIsPressed, input.useSecondaryWasReleased);
        scrollForwardOut = scrollForwardOut.After(input.scrollForwardWasPressed, input.scrollForwardIsPressed, false);
        scrollBackwardOut = scrollBackwardOut.After(input.scrollBackwardWasPressed, input.scrollBackwardIsPressed, false);
        scrollTotalOut += input.scrollInput;

        input.movementInput = Vector2.zero;
        input.scrollInput = 0f;
        input.crouchIsPressed = input.crouchWasPressed = input.crouchToggleWasPressed = false;
        input.sprintIsPressed = input.sprintToggleIsPressed = input.sprintWasPressed = input.sprintToggleWasPressed = false;
        input.jumpWasPressed = input.jumpIsPressed = false;
        input.interactWasPressed = input.interactIsPressed = input.interactWasReleased = false;
        input.dropWasPressed = input.dropIsPressed = input.dropWasReleased = false;
        input.usePrimaryWasPressed = input.usePrimaryIsPressed = input.usePrimaryWasReleased = false;
        input.useSecondaryWasPressed = input.useSecondaryIsPressed = input.useSecondaryWasReleased = false;
        input.selectSlotForwardWasPressed = input.selectSlotBackwardWasPressed = false;
        input.unselectSlotWasPressed = input.selectBackpackWasPressed = false;
        input.scrollBackwardWasPressed = input.scrollForwardWasPressed = false;
        input.scrollBackwardIsPressed = input.scrollForwardIsPressed = false;
        input.emoteIsPressed = false;
    }

    /// <summary>The Carrier's own look and mouse actions give way to the Rider's.</summary>
    private static void PlayRiderActions(CharacterInput input, bool active)
    {
        input.lookInput = Vector2.zero;
        input.pingWasPressed = false;
        input.usePrimaryWasPressed = input.usePrimaryIsPressed = input.usePrimaryWasReleased = false;
        input.useSecondaryWasPressed = input.useSecondaryIsPressed = input.useSecondaryWasReleased = false;
        input.scrollBackwardWasPressed = input.scrollForwardWasPressed = false;
        input.scrollBackwardIsPressed = input.scrollForwardIsPressed = false;
        input.scrollInput = 0f;
        if (!active || !receivedAny)
        {
            return;
        }

        ButtonFrame primary = PrimaryIn.Step();
        input.usePrimaryWasPressed = primary.WasPressed;
        input.usePrimaryIsPressed = primary.IsPressed;
        input.usePrimaryWasReleased = primary.WasReleased;

        ButtonFrame secondary = SecondaryIn.Step();
        input.useSecondaryWasPressed = secondary.WasPressed;
        input.useSecondaryIsPressed = secondary.IsPressed;
        input.useSecondaryWasReleased = secondary.WasReleased;

        input.scrollForwardWasPressed = scrollForwardIn.Presses != scrollForwardApplied;
        input.scrollForwardIsPressed = scrollForwardIn.Down;
        scrollForwardApplied = scrollForwardIn.Presses;
        input.scrollBackwardWasPressed = scrollBackwardIn.Presses != scrollBackwardApplied;
        input.scrollBackwardIsPressed = scrollBackwardIn.Down;
        scrollBackwardApplied = scrollBackwardIn.Presses;
        input.scrollInput = scrollTotalIn - scrollTotalApplied;
        scrollTotalApplied = scrollTotalIn;
    }

    /// <summary>The Carrier's body looks where the Rider looks, before it moves this frame.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Character), nameof(Character.CalculateWorldMovementDir))]
    private static void FollowRiderLook(Character __instance)
    {
        if (!__instance.IsLocal
            || lookFrame == Time.frameCount
            || !TryGetLivePair(out PairRole role, out _)
            || role != PairRole.Carrier)
        {
            return;
        }

        lookFrame = Time.frameCount;
        Vector2 look = __instance.data.lookValues;
        if (LookIn.Step(Time.deltaTime, ref look.x, ref look.y))
        {
            look.y = Mathf.Clamp(look.y, -85f, 85f);
            __instance.data.lookValues = look;
            __instance.RecalculateLookDirections();
        }
    }

    /// <summary>The Rider's hotbar keys belong to the Carrier.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterItems), "DoSwitching")]
    private static bool RiderDoesNotSwitch(CharacterItems __instance) =>
        !(__instance.character.IsLocal && TryGetLivePair(out PairRole role, out _) && role == PairRole.Rider);

    /// <summary>Called every frame after input: the Rider sends a snapshot.</summary>
    internal static void SendFromRider()
    {
        if (!TryGetLivePair(out PairRole role, out Character carrier) || role != PairRole.Rider)
        {
            return;
        }

        Vector2 look = Character.localCharacter.data.lookValues;
        var actions = new RiderActions
        {
            Yaw = look.x,
            Pitch = look.y,
            Primary = primaryOut,
            Secondary = secondaryOut,
            ScrollForward = scrollForwardOut,
            ScrollBackward = scrollBackwardOut,
            ScrollTotal = scrollTotalOut,
        };

        bool buttonsChanged = lastSent == null
            || !lastSent.Primary.Equals(actions.Primary)
            || !lastSent.Secondary.Equals(actions.Secondary)
            || !lastSent.ScrollForward.Equals(actions.ScrollForward)
            || !lastSent.ScrollBackward.Equals(actions.ScrollBackward)
            || lastSent.ScrollTotal != actions.ScrollTotal;
        if (!buttonsChanged && Time.unscaledTime < nextSend)
        {
            return;
        }

        actions.Sequence = ++sequenceOut;
        var options = new RaiseEventOptions { TargetActors = [PairRoster.ActorOf(carrier)] };
        PhotonNetwork.RaiseEvent(
            RoomKeys.RiderActionsEvent,
            actions.Encode(),
            options,
            buttonsChanged ? SendOptions.SendReliable : SendOptions.SendUnreliable);
        if (buttonsChanged)
        {
            // A click should not wait for the next network tick.
            PhotonNetwork.SendAllOutgoingCommands();
        }

        lastSent = actions;
        nextSend = Time.unscaledTime + SendInterval;
    }

    internal static void Receive(EventData photonEvent)
    {
        if (photonEvent.Code != RoomKeys.RiderActionsEvent
            || !TryGetLivePair(out PairRole role, out Character rider)
            || role != PairRole.Carrier
            || photonEvent.Sender != PairRoster.ActorOf(rider)
            || !RiderActions.TryDecode(photonEvent.CustomData, out RiderActions actions)
            || actions.Sequence <= lastSequenceIn)
        {
            return;
        }

        lastSequenceIn = actions.Sequence;
        PrimaryIn.Receive(actions.Primary);
        SecondaryIn.Receive(actions.Secondary);
        LookIn.Receive(actions.Yaw, actions.Pitch);
        if (!receivedAny)
        {
            scrollForwardApplied = actions.ScrollForward.Presses;
            scrollBackwardApplied = actions.ScrollBackward.Presses;
            scrollTotalApplied = actions.ScrollTotal;
            receivedAny = true;
            Plugin.Verbose("First Rider snapshot received.");
        }

        scrollForwardIn = actions.ScrollForward;
        scrollBackwardIn = actions.ScrollBackward;
        scrollTotalIn = actions.ScrollTotal;
    }
}

/// <summary>Drives the Control Split's network side.</summary>
internal sealed class ControlSplitLink : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private void LateUpdate() => ControlSplit.SendFromRider();

    public void OnEvent(EventData photonEvent) => ControlSplit.Receive(photonEvent);
}
