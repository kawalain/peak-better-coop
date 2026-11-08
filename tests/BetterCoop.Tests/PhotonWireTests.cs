using System;
using System.Linq;
using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

/// <summary>
/// Every payload the mod sends must hold only types Photon can serialize
/// without registration; anything else fails at send time, in game only.
/// </summary>
public class PhotonWireTests
{
    private static readonly Type[] Sendable =
    [
        typeof(byte), typeof(bool), typeof(short), typeof(int), typeof(long),
        typeof(float), typeof(double), typeof(string),
        typeof(byte[]), typeof(int[]), typeof(float[]),
    ];

    private static void AssertSendable(object payload)
    {
        if (payload is object[] parts)
        {
            Assert.All(parts, AssertSendable);
            return;
        }

        Assert.Contains(payload.GetType(), Sendable);
    }

    [Fact]
    public void The_carrier_hud_is_sendable() =>
        AssertSendable(new CarrierHud(CarrierHud.NoReticle, true, 0.5f, 3, new SlotUses(0).With(SlotUses.BackSlot, 1f)).Encode());

    [Fact]
    public void Rider_actions_are_sendable() =>
        AssertSendable(new RiderActions { Sequence = 1, Yaw = 2f, Pitch = 3f }.Encode());

    [Fact]
    public void Wheel_signals_are_sendable()
    {
        foreach (WheelSignal signal in new[]
                 {
                     WheelSignal.Open(1, true, true, 2, 4, 1),
                     WheelSignal.Close(1),
                     WheelSignal.Pick(1, WheelSlice.Slot, 0),
                     WheelSignal.Hover(1, WheelSlice.Wear, 0),
                     WheelSignal.NoHover(1),
                 })
        {
            AssertSendable(signal.Encode());
        }
    }

    [Fact]
    public void Operations_are_sendable() =>
        AssertSendable(new Operation(1, OperationKind.RoleSwap, 1.5, 1, 2, false).Encode());

    [Fact]
    public void Handover_plans_are_sendable() =>
        AssertSendable(new HandoverPlan(1, true, [new HandoverEntry(2, 1, [1f, 2f, 3f], [0.1f], 0.5f, 0f)]).Encode());

    [Fact]
    public void An_sbyte_would_be_caught() =>
        Assert.DoesNotContain(typeof(sbyte), Sendable.AsEnumerable());
}
