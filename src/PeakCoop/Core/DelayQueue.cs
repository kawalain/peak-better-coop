using System.Collections.Generic;

namespace PeakCoop.Core;

/// <summary>
/// Holds items back for a fixed delay, releasing them in the order they were
/// added. Solo Pair uses it to stand in for the network between Rider and Carrier.
/// </summary>
internal sealed class DelayQueue<T>
{
    private readonly Queue<(float At, T Item)> items = new();

    public int Count => items.Count;

    public void Enqueue(T item, float now) => items.Enqueue((now, item));

    /// <summary>Takes the oldest item once it has waited at least <paramref name="delay"/>.</summary>
    public bool TryDequeue(float now, float delay, out T item)
    {
        if (items.Count > 0 && now - items.Peek().At >= delay)
        {
            item = items.Dequeue().Item;
            return true;
        }

        item = default!;
        return false;
    }

    public void Clear() => items.Clear();
}
