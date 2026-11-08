using System;
using ExitGames.Client.Photon;
using HarmonyLib;
using BetterCoop.Core;
using BetterCoop.Net;
using BetterCoop.Ui;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Zorro.ControllerSupport;
using Zorro.Settings;

namespace BetterCoop.Features;

/// <summary>
/// The Control Split (ADR 0002): the Rider's look, use, secondary use,
/// scroll, and ping drive the Shared Body; every other action is the
/// Carrier's. The Rider's client samples those actions and sends them to the
/// Carrier, whose client plays them into the Shared Body.
/// </summary>
/// <remarks>
/// Solo Pair (a debug mode) runs both halves on one client: the player's
/// mouse goes through the Rider's path, is encoded, held back for a set
/// latency instead of crossing the network, and replayed on the Carrier's path.
/// </remarks>
[HarmonyPatch]
internal static class ControlSplit
{
    private enum Mode
    {
        None,
        Carrier,
        Rider,
        Solo,
    }

    private const float SendInterval = 1f / 30f;
    private const int SoloPartner = -2;

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

    // Solo Pair: the Rider's look kept apart from the body, and the stand-in network.
    private static readonly DelayQueue<byte[]> SoloWire = new();
    private static Vector2 soloLook;
    private static bool soloAnnounced;

    private static int partnerActor = -1;
    private static Character? linkedLocal;
    private static Character? linkedPartner;

    /// <summary>Whether Solo Pair applies: switched on and alone in a room.</summary>
    internal static bool SoloPairWanted =>
        CoopConfig.SoloPair.Value && PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.PlayerCount == 1;

    /// <summary>
    /// Whether the local player has the Carrier's side of a Shared Body: a
    /// Carrier with a living Rider, or Solo Pair, where the Rider is the player too.
    /// </summary>
    internal static bool LocalCarrier(out Character rider, out bool solo)
    {
        Mode mode = CurrentMode(out rider);
        solo = mode == Mode.Solo;
        return mode == Mode.Carrier || solo;
    }

    private static Mode CurrentMode(out Character partner)
    {
        Character local = Character.localCharacter;
        partner = null!;
        bool soloWanted = SoloPairWanted;
        if (soloWanted != soloAnnounced)
        {
            // Announce the setting or the room changing, not every death or scene load.
            soloAnnounced = soloWanted;
            CoopNotice.Info(soloWanted ? Strings.SoloPairOn(CoopConfig.SoloPairLatency.Value) : Strings.SoloPairOff);
        }

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

        if (soloWanted)
        {
            partner = local;
            if (Enter(SoloPartner, local, local, "Solo Pair"))
            {
                soloLook = local.data.lookValues;
            }

            return Mode.Solo;
        }

        Forget();
        return Mode.None;
    }

    /// <summary>
    /// Starts a fresh link to a partner; true when it changed. A respawned
    /// character (each scene load spawns new ones) also starts a fresh link.
    /// </summary>
    private static bool Enter(int actor, Character local, Character partner, string label)
    {
        if (actor == partnerActor
            && ReferenceEquals(local, linkedLocal)
            && ReferenceEquals(partner, linkedPartner))
        {
            return false;
        }

        Forget();
        partnerActor = actor;
        linkedLocal = local;
        linkedPartner = partner;
        Plugin.Verbose($"Control Split active as {label} (partner {actor}).");
        return true;
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
        SoloWire.Clear();
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
            case Mode.Solo:
                if (!BackpackWheelSplit.HoldsRiderButtons())
                {
                    CaptureRiderActions(__instance);
                }

                CaptureSoloLook(local, __instance);
                DeliverSoloWire();
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

    /// <summary>Turns the mouse into the Rider's look, as the game's CameraLook would.</summary>
    private static void CaptureSoloLook(Character local, CharacterInput input)
    {
        CharacterMovement movement = local.refs.movement;
        bool mouse = InputHandler.GetCurrentUsedInputScheme() == InputScheme.KeyboardMouse;
        float sensitivity = mouse ? movement.mouseSensSetting.Value : movement.controllerSensSetting.Value;
        float invertX = movement.invertXSetting.Value == OffOnMode.OFF ? 1f : -1f;
        float invertY = movement.invertYSetting.Value == OffOnMode.OFF ? 1f : -1f;
        soloLook.x += input.lookInput.x * sensitivity * invertX;
        soloLook.y = Mathf.Clamp(soloLook.y + (input.lookInput.y * sensitivity * invertY), -85f, 85f);
    }

    private static void DeliverSoloWire()
    {
        float delay = CoopConfig.SoloPairLatency.Value / 1000f;
        while (SoloWire.TryDequeue(Time.unscaledTime, delay, out byte[] payload))
        {
            Accept(payload);
        }
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

        Mode mode = CurrentMode(out _);
        if (mode != Mode.Carrier && mode != Mode.Solo)
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
        !(__instance.character.IsLocal && CurrentMode(out _) == Mode.Rider);

    /// <summary>Called every frame after input: the Rider sends a snapshot.</summary>
    internal static void SendFromRider()
    {
        Mode mode = CurrentMode(out Character partner);
        if (mode != Mode.Rider && mode != Mode.Solo)
        {
            return;
        }

        Vector2 look = mode == Mode.Solo ? soloLook : Character.localCharacter.data.lookValues;
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

        if (mode == Mode.Solo)
        {
            SoloWire.Enqueue(actions.Encode(), Time.unscaledTime);
            return;
        }

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
