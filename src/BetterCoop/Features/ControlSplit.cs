using System;
using ExitGames.Client.Photon;
using HarmonyLib;
using BetterCoop.Core;
using BetterCoop.Net;
using BetterCoop.Ui;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace BetterCoop.Features;

/// <summary>
/// The Control Split (ADR 0002): the Rider's look, use, secondary use,
/// scroll, and ping drive the Shared Body; every other action is the
/// Carrier's. The Rider's client samples those actions and sends them to the
/// Carrier, whose client plays them into the Shared Body.
/// </summary>
[HarmonyPatch]
[HarmonyWrapSafe]
internal static class ControlSplit
{
    private enum Mode
    {
        None,
        Carrier,
        Rider,
    }

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
    private static readonly RelayedPresses ScrollForwardPresses = new();
    private static readonly RelayedPresses ScrollBackwardPresses = new();
    private static bool scrollForwardHeld;
    private static bool scrollBackwardHeld;
    private static float scrollTotalIn;
    private static float scrollTotalApplied;
    private static bool receivedAny;
    private static int lastSequenceIn = int.MinValue;
    private static int lookFrame = -1;

    private static int partnerActor = -1;
    private static Character? linkedLocal;
    private static Character? linkedPartner;

    /// <summary>Whether the local player has the Carrier's side of a Shared Body, with a living Rider.</summary>
    internal static bool LocalCarrier(out Character rider) => CurrentMode(out rider) == Mode.Carrier;

    private static Mode CurrentMode(out Character partner)
    {
        Character local = Character.localCharacter;
        partner = null!;

        if (local == null || local.data.dead)
        {
            Forget();
            return Mode.None;
        }

        PairRole role = PairRoster.RoleOf(local);
        if (role != PairRole.None)
        {
            if (!PairRoster.TryGetPartner(local, out partner) || partner.data.dead)
            {
                Forget();
                return Mode.None;
            }

            Enter(PairRoster.ActorOf(partner), local, partner, role.ToString());
            return role == PairRole.Carrier ? Mode.Carrier : Mode.Rider;
        }

        Forget();
        return Mode.None;
    }

    /// <summary>
    /// Starts a fresh link to a partner, unless it is the current one. A
    /// respawned character (each scene load spawns new ones) also starts a
    /// fresh link.
    /// </summary>
    private static void Enter(int actor, Character local, Character partner, string label)
    {
        if (actor == partnerActor
            && ReferenceEquals(local, linkedLocal)
            && ReferenceEquals(partner, linkedPartner))
        {
            return;
        }

        Forget();
        partnerActor = actor;
        linkedLocal = local;
        linkedPartner = partner;
        Plugin.Verbose($"Control Split active as {label} (partner {actor}).");
    }

    private static void Forget()
    {
        if (partnerActor == -1)
        {
            return;
        }

        partnerActor = -1;
        linkedLocal = null;
        linkedPartner = null;
        lastSent = null;
        PrimaryIn.Reset();
        SecondaryIn.Reset();
        ScrollForwardPresses.Reset();
        ScrollBackwardPresses.Reset();
        LookIn.Reset();
        receivedAny = false;
        lastSequenceIn = int.MinValue;
    }

    /// <summary>
    /// Runs before any other patch on sampling, so the Rider's actions are
    /// read as the player made them even where another patch clears a carried
    /// player's input afterwards.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(CharacterInput), nameof(CharacterInput.Sample))]
    private static void SplitSampledInput(CharacterInput __instance, bool playerMovementActive)
    {
        Character local = Character.localCharacter;
        if (local == null || local.input != __instance)
        {
            return;
        }

        switch (CurrentMode(out _))
        {
            case Mode.Rider:
                RiderIntent.Capture(__instance);
                if (!BackpackWheelSplit.HoldsRiderButtons())
                {
                    CaptureRiderActions(__instance);
                }

                RiderBinoculars.Zoom(__instance);
                DropCarrierActions(__instance);
                break;
            case Mode.Carrier:
                RiderIntent.Clear();
                PlayRiderActions(__instance, playerMovementActive);
                break;
        }
    }

    private static void CaptureRiderActions(CharacterInput input)
    {
        primaryOut = primaryOut.After(input.usePrimaryWasPressed, input.usePrimaryIsPressed, input.usePrimaryWasReleased);
        secondaryOut = secondaryOut.After(input.useSecondaryWasPressed, input.useSecondaryIsPressed, input.useSecondaryWasReleased);
        scrollForwardOut = scrollForwardOut.After(input.scrollForwardWasPressed, input.scrollForwardIsPressed, false);
        scrollBackwardOut = scrollBackwardOut.After(input.scrollBackwardWasPressed, input.scrollBackwardIsPressed, false);
        scrollTotalOut += input.scrollInput;
    }

    /// <summary>The Rider keeps look, ping, pause, and push-to-talk; the rest is the Carrier's.</summary>
    private static void DropCarrierActions(CharacterInput input)
    {
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
        if (!receivedAny)
        {
            return;
        }

        if (!active)
        {
            // A menu or wheel blocks the Carrier's input; the game drops the
            // Carrier's own clicks meanwhile, so the Rider's do not pile up.
            PrimaryIn.Discard();
            SecondaryIn.Discard();
            ScrollForwardPresses.Discard();
            ScrollBackwardPresses.Discard();
            scrollTotalApplied = scrollTotalIn;
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
        if (primary.WasPressed || primary.WasReleased || secondary.WasPressed || secondary.WasReleased)
        {
            // A grab, use, or throw aims along the latest look that arrived.
            LookIn.SnapNext();
        }

        input.scrollForwardWasPressed = ScrollForwardPresses.Step();
        input.scrollForwardIsPressed = scrollForwardHeld;
        input.scrollBackwardWasPressed = ScrollBackwardPresses.Step();
        input.scrollBackwardIsPressed = scrollBackwardHeld;
        input.scrollInput = scrollTotalIn - scrollTotalApplied;
        scrollTotalApplied = scrollTotalIn;
    }

    /// <summary>The Carrier's body looks where the Rider looks, before it moves this frame.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Character), nameof(Character.CalculateWorldMovementDir))]
    private static void FollowRiderLook(Character __instance)
    {
        if (!__instance.IsLocal || lookFrame == Time.frameCount)
        {
            return;
        }

        if (GUIManager.instance != null && GUIManager.instance.backpackWheel.gameObject.activeSelf)
        {
            // The game holds a body still while its owner picks from a backpack wheel.
            return;
        }

        if (CurrentMode(out _) != Mode.Carrier)
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

    /// <summary>
    /// The Rider's use button grabs and lets go of the wall, so the Rider's
    /// own Climb Input (Hold or Toggle) decides how, not the Carrier's: a
    /// climb the Rider's grab just started keeps its toggle only if the
    /// Rider climbs by Toggle.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CharacterClimbing), nameof(CharacterClimbing.TryToStartWallClimb))]
    private static void FollowRiderClimbInput(CharacterClimbing __instance)
    {
        if (__instance.sinceLastClimbStarted == 0f
            && __instance.character.IsLocal
            && LocalCarrier(out Character rider)
            && rider.photonView.Owner is Photon.Realtime.Player owner)
        {
            __instance.climbToggledOn = ModPresence.ClimbTogglesOf(owner);
        }
    }

    private static RopeSpool? latchedSpool;

    private static float latchedScroll;

    /// <summary>
    /// A rope spool reads the scroll in its update and uses it in the next
    /// physics step. The Rider's scroll arrives as one frame's worth per
    /// network message, and a frame with no physics step after it loses that
    /// scroll, so unspooling worked only some of the time. On the Carrier's
    /// client the spool keeps the Rider's scroll until a physics step has
    /// used it. Only the spool is held this way: other items scroll once per
    /// frame and would count a held scroll again.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RopeSpool), "Update")]
    private static void HoldRiderScrollForSpool(RopeSpool __instance)
    {
        if (!LocalCarrier(out _)
            || __instance.item.holderCharacter != Character.localCharacter
            || !__instance.photonView.IsMine)
        {
            latchedSpool = null;
            return;
        }

        if (__instance.scroll != 0f || __instance.ropeTier.LookingToPlaceAnchor)
        {
            latchedSpool = __instance.scroll != 0f ? __instance : null;
            latchedScroll = __instance.scroll;
        }
        else if (latchedSpool == __instance)
        {
            __instance.scroll = latchedScroll;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RopeSpool), "FixedUpdate")]
    private static void SpoolUsedScroll(RopeSpool __instance)
    {
        if (latchedSpool == __instance)
        {
            latchedSpool = null;
        }
    }

    /// <summary>The Rider's hotbar keys belong to the Carrier.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterItems), "DoSwitching")]
    private static bool RiderDoesNotSwitch(CharacterItems __instance) =>
        !(__instance.character.IsLocal && CurrentMode(out _) == Mode.Rider);

    /// <summary>Called every frame after input: the Rider sends a snapshot.</summary>
    internal static void SendFromRider()
    {
        if (CurrentMode(out Character partner) != Mode.Rider)
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
        lastSent = actions;
        nextSend = Time.unscaledTime + SendInterval;

        var options = new RaiseEventOptions { TargetActors = [PairRoster.ActorOf(partner)] };
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
    }

    internal static void Receive(EventData photonEvent)
    {
        if (photonEvent.Code == RoomKeys.RiderActionsEvent
            && CurrentMode(out Character rider) == Mode.Carrier
            && photonEvent.Sender == PairRoster.ActorOf(rider))
        {
            Accept(photonEvent.CustomData);
        }
    }

    private static void Accept(object? payload)
    {
        if (!RiderActions.TryDecode(payload, out RiderActions actions) || actions.Sequence <= lastSequenceIn)
        {
            return;
        }

        lastSequenceIn = actions.Sequence;
        PrimaryIn.Receive(actions.Primary);
        SecondaryIn.Receive(actions.Secondary);
        ScrollForwardPresses.Receive(actions.ScrollForward.Presses);
        ScrollBackwardPresses.Receive(actions.ScrollBackward.Presses);
        LookIn.Receive(actions.Yaw, actions.Pitch);
        if (!receivedAny)
        {
            scrollTotalApplied = actions.ScrollTotal;
            receivedAny = true;
            Plugin.Verbose("First Rider snapshot received.");
        }

        scrollForwardHeld = actions.ScrollForward.Down;
        scrollBackwardHeld = actions.ScrollBackward.Down;
        scrollTotalIn = actions.ScrollTotal;
    }
}

/// <summary>Drives the Control Split's network side.</summary>
internal sealed class ControlSplitLink : MonoBehaviourPunCallbacks, IOnEventCallback
{
    // Each step is guarded on its own, so one failing feature leaves the others running.
    private void LateUpdate()
    {
        try
        {
            ControlSplit.SendFromRider();
        }
        catch (Exception exception)
        {
            Guard.Report("ControlSplit.SendFromRider", exception);
        }
        try
        {
            UseFeedback.SendFromCarrier();
        }
        catch (Exception exception)
        {
            Guard.Report("UseFeedback.SendFromCarrier", exception);
        }
        try
        {
            BackpackWheelSplit.Tick();
        }
        catch (Exception exception)
        {
            Guard.Report("BackpackWheelSplit.Tick", exception);
        }
    }

    public void OnEvent(EventData photonEvent)
    {
        try
        {
            ControlSplit.Receive(photonEvent);
        }
        catch (Exception exception)
        {
            Guard.Report("ControlSplit.Receive", exception);
        }
        try
        {
            UseFeedback.Receive(photonEvent);
        }
        catch (Exception exception)
        {
            Guard.Report("UseFeedback.Receive", exception);
        }
        try
        {
            SharedViewHud.ReceiveSlotUses(photonEvent);
        }
        catch (Exception exception)
        {
            Guard.Report("SharedViewHud.ReceiveSlotUses", exception);
        }
        try
        {
            SharedViewHud.Receive(photonEvent);
        }
        catch (Exception exception)
        {
            Guard.Report("SharedViewHud.Receive", exception);
        }
        try
        {
            SharedViewSounds.Receive(photonEvent);
        }
        catch (Exception exception)
        {
            Guard.Report("SharedViewSounds.Receive", exception);
        }
        try
        {
            BackpackWheelSplit.Receive(photonEvent);
        }
        catch (Exception exception)
        {
            Guard.Report("BackpackWheelSplit.Receive", exception);
        }
    }
}
