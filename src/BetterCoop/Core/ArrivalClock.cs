using System.Collections.Generic;

namespace BetterCoop.Core;

/// <summary>
/// When each player's current character appeared. Other mods move and revive
/// a player who joins during a Run in the first seconds after the character
/// spawns, so a Late Pairing waits until that is over.
/// </summary>
internal sealed class ArrivalClock
{
    /// <summary>How long a new character settles before it can be paired.</summary>
    public const float SettleSeconds = 5f;

    private readonly Dictionary<int, (int Body, float Since)> seen = new();

    /// <summary>Whether <paramref name="body"/>, the actor's character now, has been there long enough.</summary>
    public bool Settled(int actor, int body, float now)
    {
        if (!seen.TryGetValue(actor, out (int Body, float Since) entry) || entry.Body != body)
        {
            seen[actor] = (body, now);
            return false;
        }

        return now - entry.Since >= SettleSeconds;
    }

    public void Clear() => seen.Clear();
}
