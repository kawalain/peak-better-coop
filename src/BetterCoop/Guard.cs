using System;
using System.Collections.Generic;

namespace BetterCoop;

/// <summary>
/// Reports an exception caught at one of the mod's own entry points: a
/// Unity tick or a Photon callback. The caller keeps running, so one broken
/// step neither stops the rest of the frame nor reaches the game or another
/// mod's callbacks. Each place and exception type is logged in full once,
/// then counted, so a failure that repeats every frame stays one entry.
/// </summary>
internal static class Guard
{
    private const int RepeatNote = 600;

    private static readonly Dictionary<(string Site, Type Type), int> Seen = new();

    internal static void Report(string site, Exception exception)
    {
        var key = (site, exception.GetType());
        Seen.TryGetValue(key, out int count);
        Seen[key] = ++count;
        if (count == 1)
        {
            Plugin.Log.LogError($"{site} failed; it keeps running.\n{exception}");
        }
        else if (count % RepeatNote == 0)
        {
            Plugin.Log.LogError($"{site} failed {count} times with {exception.GetType().Name}: {exception.Message}");
        }
    }
}
