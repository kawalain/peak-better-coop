using System.Collections.Generic;

namespace BetterCoop.Core;

/// <summary>Stepping through a list of answers, round from the last to the first.</summary>
internal static class Steps
{
    public static T Next<T>(IReadOnlyList<T> choices, T current, int by)
    {
        if (choices.Count == 0)
        {
            return current;
        }

        int index = -1;
        for (int i = 0; i < choices.Count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(choices[i], current))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            return by >= 0 ? choices[0] : choices[choices.Count - 1];
        }

        return choices[((index + by) % choices.Count + choices.Count) % choices.Count];
    }
}
