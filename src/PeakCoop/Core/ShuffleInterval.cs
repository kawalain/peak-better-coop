using System;

namespace PeakCoop.Core;

/// <summary>How often a Timed Shuffle comes, in whole minutes.</summary>
internal static class ShuffleInterval
{
    public const int MinMinutes = 1;

    public const int MaxMinutes = 60;

    public const int DefaultMinutes = 10;

    /// <summary>The interval to run, whatever was set or received.</summary>
    public static int Clamp(int minutes) => Math.Min(MaxMinutes, Math.Max(MinMinutes, minutes));
}
