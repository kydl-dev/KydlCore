using System;
using System.Collections.Generic;

namespace KydlCore.Features.Diagnostics.Domain;

/// <summary>
/// A fixed-size ring that keeps only the newest items (the flight recorder keeps the last N samples of every tracked object and nothing older).
/// Pure C#: no Unity, no game types, no logging, so it is unit tested. Not thread-safe: the owner locks.
/// </summary>
/// <typeparam name="T">The item type (a sample or a note).</typeparam>
internal sealed class FlightRing<T>
{
    readonly T[] _items;
    int _next;
    int _count;

    /// <summary>Creates an empty ring.</summary>
    /// <param name="capacity">How many items it keeps (at least 1).</param>
    internal FlightRing(int capacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "a ring keeps at least one item");
        }

        _items = new T[capacity];
    }

    /// <summary>How many items the ring keeps at most.</summary>
    internal int Capacity => _items.Length;

    /// <summary>How many items it holds now.</summary>
    internal int Count => _count;

    /// <summary>The newest item, or the default value while the ring is empty.</summary>
    internal T Newest => _count == 0 ? default : _items[(_next - 1 + _items.Length) % _items.Length];

    /// <summary>Adds an item; the oldest one is dropped when the ring is full.</summary>
    /// <returns>The count afterwards (never more than <see cref="Capacity"/>).</returns>
    internal int Add(T item)
    {
        _items[_next] = item;
        _next = (_next + 1) % _items.Length;
        if (_count < _items.Length)
        {
            _count++;
        }

        return _count;
    }

    /// <summary>The items, oldest first, that pass <paramref name="keep"/> (null = all). A copy: changing it changes nothing.</summary>
    internal List<T> Items(Func<T, bool> keep = null)
    {
        var list = new List<T>(_count);
        for (int i = 0; i < _count; i++)
        {
            T item = _items[(_next - _count + i + _items.Length) % _items.Length];
            if (keep == null || keep(item))
            {
                list.Add(item);
            }
        }

        return list;
    }
}
