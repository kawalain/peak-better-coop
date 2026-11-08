namespace PeakCoop.Core;

/// <summary>How often a Timed Shuffle comes, in minutes.</summary>
public enum ShuffleInterval : byte
{
    OneMinute = 1,
    FiveMinutes = 5,
    TenMinutes = 10,
    ThirtyMinutes = 30,
}
