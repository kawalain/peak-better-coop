using System;

namespace BetterCoop.Core;

/// <summary>
/// One snapshot of the Rider's actions, sent to the Carrier. Button presses
/// and releases are cumulative counters, so any later snapshot carries every
/// earlier edge and a lost packet loses nothing.
/// </summary>
internal sealed class RiderActions
{
    public const int Size = 4 + 4 + 4 + 1 + (6 * 2) + 4;

    [Flags]
    private enum Held : byte
    {
        None = 0,
        Primary = 1,
        Secondary = 2,
        ScrollForward = 4,
        ScrollBackward = 8,
    }

    public int Sequence { get; set; }

    public float Yaw { get; set; }

    public float Pitch { get; set; }

    public ButtonCounters Primary { get; set; }

    public ButtonCounters Secondary { get; set; }

    public ButtonCounters ScrollForward { get; set; }

    public ButtonCounters ScrollBackward { get; set; }

    /// <summary>Sum of every scroll delta the Rider produced.</summary>
    public float ScrollTotal { get; set; }

    public byte[] Encode()
    {
        var buffer = new byte[Size];
        int offset = 0;
        Write(buffer, ref offset, BitConverter.GetBytes(Sequence));
        Write(buffer, ref offset, BitConverter.GetBytes(Yaw));
        Write(buffer, ref offset, BitConverter.GetBytes(Pitch));

        Held held = Held.None;
        if (Primary.Down) held |= Held.Primary;
        if (Secondary.Down) held |= Held.Secondary;
        if (ScrollForward.Down) held |= Held.ScrollForward;
        if (ScrollBackward.Down) held |= Held.ScrollBackward;
        buffer[offset++] = (byte)held;

        Write(buffer, ref offset, BitConverter.GetBytes(Primary.Presses));
        Write(buffer, ref offset, BitConverter.GetBytes(Primary.Releases));
        Write(buffer, ref offset, BitConverter.GetBytes(Secondary.Presses));
        Write(buffer, ref offset, BitConverter.GetBytes(Secondary.Releases));
        Write(buffer, ref offset, BitConverter.GetBytes(ScrollForward.Presses));
        Write(buffer, ref offset, BitConverter.GetBytes(ScrollBackward.Presses));
        Write(buffer, ref offset, BitConverter.GetBytes(ScrollTotal));
        return buffer;
    }

    public static bool TryDecode(object? payload, out RiderActions actions)
    {
        actions = new RiderActions();
        if (payload is not byte[] buffer || buffer.Length != Size)
        {
            return false;
        }

        int offset = 0;
        actions.Sequence = BitConverter.ToInt32(buffer, offset);
        offset += 4;
        actions.Yaw = BitConverter.ToSingle(buffer, offset);
        offset += 4;
        actions.Pitch = BitConverter.ToSingle(buffer, offset);
        offset += 4;
        var held = (Held)buffer[offset++];

        ushort Next()
        {
            ushort value = BitConverter.ToUInt16(buffer, offset);
            offset += 2;
            return value;
        }

        actions.Primary = new ButtonCounters((held & Held.Primary) != 0, Next(), Next());
        actions.Secondary = new ButtonCounters((held & Held.Secondary) != 0, Next(), Next());
        actions.ScrollForward = new ButtonCounters((held & Held.ScrollForward) != 0, Next(), 0);
        actions.ScrollBackward = new ButtonCounters((held & Held.ScrollBackward) != 0, Next(), 0);
        actions.ScrollTotal = BitConverter.ToSingle(buffer, offset);

        // A look that is not a number would carry the Shared Body's camera off with it.
        return WireLimits.IsFinite(actions.Yaw) && WireLimits.IsFinite(actions.Pitch) && WireLimits.IsFinite(actions.ScrollTotal);
    }

    private static void Write(byte[] buffer, ref int offset, byte[] bytes)
    {
        Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
        offset += bytes.Length;
    }
}

/// <summary>A button's state and how often it went down and up.</summary>
internal readonly struct ButtonCounters : IEquatable<ButtonCounters>
{
    public ButtonCounters(bool down, ushort presses, ushort releases)
    {
        Down = down;
        Presses = presses;
        Releases = releases;
    }

    public bool Down { get; }

    public ushort Presses { get; }

    public ushort Releases { get; }

    /// <summary>The counters after one frame of input.</summary>
    public ButtonCounters After(bool wasPressed, bool isPressed, bool wasReleased) => new(
        isPressed,
        (ushort)(Presses + (wasPressed ? 1 : 0)),
        (ushort)(Releases + (wasReleased ? 1 : 0)));

    public bool Equals(ButtonCounters other) =>
        Down == other.Down && Presses == other.Presses && Releases == other.Releases;

    public override bool Equals(object? obj) => obj is ButtonCounters other && Equals(other);

    public override int GetHashCode() => (Down ? 1 : 0) ^ (Presses << 1) ^ (Releases << 17);
}
