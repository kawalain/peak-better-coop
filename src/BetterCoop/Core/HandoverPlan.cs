using System;
using System.Collections.Generic;

namespace BetterCoop.Core;

/// <summary>What one new Carrier takes over from a former Shared Body.</summary>
internal sealed class HandoverEntry
{
    public HandoverEntry(int carrier, int source, float[] place, float[] statuses, float stamina, float extraStamina)
    {
        Carrier = carrier;
        Source = source;
        Place = place;
        Statuses = statuses;
        Stamina = stamina;
        ExtraStamina = extraStamina;
    }

    public int Carrier { get; }

    /// <summary>The former Shared Body's Carrier; the Carrier itself when nothing changes.</summary>
    public int Source { get; }

    /// <summary>Where the former Shared Body stood (x, y, z).</summary>
    public float[] Place { get; }

    public float[] Statuses { get; }

    public float Stamina { get; }

    public float ExtraStamina { get; }
}

/// <summary>
/// The Body Handover of one operation, published with the new Pairs in one
/// room property write, so every client sees both at once. Each client
/// applies its own part once per <see cref="Id"/>.
/// </summary>
internal sealed class HandoverPlan
{
    public HandoverPlan(int id, bool movesPlaces, IReadOnlyList<HandoverEntry> entries)
    {
        Id = id;
        MovesPlaces = movesPlaces;
        Entries = entries;
    }

    /// <summary>The operation this plan belongs to.</summary>
    public int Id { get; }

    /// <summary>Whether new Carriers move to their former Shared Body's place.</summary>
    public bool MovesPlaces { get; }

    public IReadOnlyList<HandoverEntry> Entries { get; }

    public HandoverEntry? For(int carrier)
    {
        foreach (HandoverEntry entry in Entries)
        {
            if (entry.Carrier == carrier)
            {
                return entry;
            }
        }

        return null;
    }

    public object[] Encode()
    {
        int count = Entries.Count;
        int statusCount = count == 0 ? 0 : Entries[0].Statuses.Length;
        var carriers = new int[count];
        var sources = new int[count];
        var places = new float[count * 3];
        var statuses = new float[count * statusCount];
        var stamina = new float[count * 2];
        for (int i = 0; i < count; i++)
        {
            HandoverEntry entry = Entries[i];
            carriers[i] = entry.Carrier;
            sources[i] = entry.Source;
            Array.Copy(entry.Place, 0, places, i * 3, 3);
            Array.Copy(entry.Statuses, 0, statuses, i * statusCount, statusCount);
            stamina[i * 2] = entry.Stamina;
            stamina[(i * 2) + 1] = entry.ExtraStamina;
        }

        return [Id, MovesPlaces, statusCount, carriers, sources, places, statuses, stamina];
    }

    public static HandoverPlan? Decode(object? value)
    {
        if (value is not object[] { Length: 8 } parts
            || parts[0] is not int id
            || parts[1] is not bool movesPlaces
            || parts[2] is not int statusCount
            || parts[3] is not int[] carriers
            || parts[4] is not int[] sources
            || parts[5] is not float[] places
            || parts[6] is not float[] statuses
            || parts[7] is not float[] stamina)
        {
            return null;
        }

        int count = carriers.Length;
        if (sources.Length != count
            || places.Length != count * 3
            || statusCount < 0
            || statuses.Length != count * statusCount
            || stamina.Length != count * 2)
        {
            return null;
        }

        var entries = new List<HandoverEntry>(count);
        for (int i = 0; i < count; i++)
        {
            var place = new float[3];
            Array.Copy(places, i * 3, place, 0, 3);
            var own = new float[statusCount];
            Array.Copy(statuses, i * statusCount, own, 0, statusCount);
            entries.Add(new HandoverEntry(carriers[i], sources[i], place, own, stamina[i * 2], stamina[(i * 2) + 1]));
        }

        return new HandoverPlan(id, movesPlaces, entries);
    }
}
