namespace BetterCoop.Core;

/// <summary>What a signal about the backpack wheel says.</summary>
internal enum WheelSignalKind
{
    /// <summary>Carrier to Rider: the Carrier opened a backpack's wheel and holds it open.</summary>
    Open = 1,

    /// <summary>Carrier to Rider: the wheel closed, picked or not.</summary>
    Close = 2,

    /// <summary>Rider to Carrier: the slice the Rider picked.</summary>
    Pick = 3,

    /// <summary>Either way: the slice the picking side points at, or none, for the watching side's wheel.</summary>
    Hover = 4,
}

/// <summary>The kinds of slice in the game's backpack wheel.</summary>
internal enum WheelSlice
{
    /// <summary>One of the backpack's slots: take its item, or stash the held one there.</summary>
    Slot = 0,

    /// <summary>Put on a backpack lying on the ground.</summary>
    Wear = 1,

    /// <summary>Stash the held item.</summary>
    Stash = 2,

    /// <summary>Turn the held item into jetpack fuel.</summary>
    Fuel = 3,
}

/// <summary>
/// One message of the backpack wheel's Control Split, sent between a Pair.
/// Each opening of the wheel is a session with its own number, so a pick, a
/// hover, or a close that crosses a newer opening is ignored.
/// </summary>
internal readonly struct WheelSignal
{
    private const int Size = 6;

    private const int EquippedBit = 1;

    private const int RiderPicksBit = 2;

    private const int NoSlice = -1;

    private WheelSignal(WheelSignalKind kind, int session, int a, int b, int c, int d)
    {
        Kind = kind;
        Session = session;
        A = a;
        B = b;
        C = c;
        D = d;
    }

    public WheelSignalKind Kind { get; }

    public int Session { get; }

    /// <summary>Open: whether the backpack is worn rather than lying as an item.</summary>
    public bool Equipped => (A & EquippedBit) != 0;

    /// <summary>Open: whether the Rider picks; otherwise the Carrier picks and the Rider watches.</summary>
    public bool RiderPicks => (A & RiderPicksBit) != 0;

    /// <summary>Open: the network view of the backpack item, or of the character wearing it.</summary>
    public int ViewId => B;

    /// <summary>Open: how many slots the wheel shows.</summary>
    public int SlotCount => C;

    /// <summary>Open: the game's backpack type.</summary>
    public byte BackpackType => (byte)D;

    /// <summary>Hover: whether the picking side points at a slice at all.</summary>
    public bool HasSlice => A != NoSlice;

    /// <summary>Pick and Hover: which kind of slice.</summary>
    public WheelSlice Slice => (WheelSlice)A;

    /// <summary>Pick and Hover: the slot of a slot slice.</summary>
    public byte SlotId => (byte)B;

    private int A { get; }

    private int B { get; }

    private int C { get; }

    private int D { get; }

    public static WheelSignal Open(int session, bool equipped, bool riderPicks, int viewId, int slotCount, byte backpackType) =>
        new(
            WheelSignalKind.Open,
            session,
            (equipped ? EquippedBit : 0) | (riderPicks ? RiderPicksBit : 0),
            viewId,
            slotCount,
            backpackType);

    public static WheelSignal Close(int session) => new(WheelSignalKind.Close, session, 0, 0, 0, 0);

    public static WheelSignal Pick(int session, WheelSlice slice, byte slotId) =>
        new(WheelSignalKind.Pick, session, (int)slice, slotId, 0, 0);

    public static WheelSignal Hover(int session, WheelSlice slice, byte slotId) =>
        new(WheelSignalKind.Hover, session, (int)slice, slotId, 0, 0);

    public static WheelSignal NoHover(int session) => new(WheelSignalKind.Hover, session, NoSlice, 0, 0, 0);

    /// <summary>A number that tells pointed-at slices apart, or -1 for none; for noticing a change.</summary>
    public static int HoverKey(WheelSlice? slice, byte slotId) => slice is WheelSlice s ? ((int)s << 8) | slotId : NoSlice;

    public int[] Encode() => [(int)Kind, Session, A, B, C, D];

    public static bool TryDecode(object? payload, out WheelSignal signal)
    {
        signal = default;
        if (payload is not int[] values || values.Length != Size)
        {
            return false;
        }

        var kind = (WheelSignalKind)values[0];
        bool valid = kind switch
        {
            WheelSignalKind.Open => values[2] is >= 0 and <= (EquippedBit | RiderPicksBit)
                && values[4] is >= 0 and <= byte.MaxValue
                && values[5] is >= 0 and <= byte.MaxValue,
            WheelSignalKind.Close => true,
            WheelSignalKind.Pick => IsSlice(values[2]) && IsSlot(values[3]),
            WheelSignalKind.Hover => (values[2] == NoSlice || IsSlice(values[2])) && IsSlot(values[3]),
            _ => false,
        };
        if (!valid)
        {
            return false;
        }

        signal = new WheelSignal(kind, values[1], values[2], values[3], values[4], values[5]);
        return true;
    }

    private static bool IsSlice(int value) => value is >= (int)WheelSlice.Slot and <= (int)WheelSlice.Fuel;

    private static bool IsSlot(int value) => value is >= 0 and <= byte.MaxValue;
}
