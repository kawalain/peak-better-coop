namespace BetterCoop.Core;

/// <summary>
/// Arithmetic on Photon server time. The server clock counts milliseconds in
/// an unsigned 32-bit number, so in seconds it wraps to 0 about every 49.7
/// days, and a Run may cross that moment. Times are compared through the
/// shortest way around the wrap instead of by plain subtraction.
/// </summary>
internal static class ServerClock
{
    /// <summary>The seconds after which server time starts again at 0.</summary>
    public const double Span = 4294967.296;

    /// <summary>The seconds from <paramref name="then"/> to <paramref name="now"/>: negative when <paramref name="then"/> is still ahead.</summary>
    public static double Since(double now, double then)
    {
        double seconds = (now - then) % Span;
        if (seconds > Span / 2)
        {
            return seconds - Span;
        }

        return seconds <= -Span / 2 ? seconds + Span : seconds;
    }

    /// <summary>The later of two server times.</summary>
    public static double Later(double a, double b) => Since(a, b) >= 0 ? a : b;

    /// <summary>The later of two server times, either of which may be missing.</summary>
    public static double? Later(double? a, double? b) =>
        a is double first && b is double second ? Later(first, second) : a ?? b;

    /// <summary>How far ahead of this client's server time another client's may run.</summary>
    public const double Skew = 5;

    /// <summary>
    /// Whether <paramref name="then"/> lies within the last <paramref name="seconds"/>
    /// before <paramref name="now"/>; a time further ahead than clocks drift
    /// apart was not stamped now, so it is not recent.
    /// </summary>
    public static bool IsRecent(double now, double then, double seconds)
    {
        double since = Since(now, then);
        return since >= -Skew && since <= seconds;
    }

    /// <summary>Whether a received server time is a number at all.</summary>
    public static bool IsTime(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
