using System.Collections.Generic;
using System.Linq;

namespace BetterCoop.Core;

/// <summary>
/// Who can be in a Pair when more players who can pair are in the room than
/// the mod pairs at once (<see cref="WireLimits.MaxScouts"/>): those who
/// joined first, by actor number. The rest climb as Unpaired Players until
/// a seat frees up.
/// </summary>
internal static class PairSeats
{
    public static HashSet<int> Seated(IEnumerable<int> pairable, int seats = WireLimits.MaxScouts) =>
        new(pairable.Distinct().OrderBy(actor => actor).Take(seats));
}
