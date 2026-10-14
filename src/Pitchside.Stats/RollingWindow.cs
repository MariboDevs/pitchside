using System.Collections.Immutable;

namespace Pitchside.Stats;

/// <summary>
/// Items from the last <see cref="SpanMs"/> milliseconds of period clock, oldest first. Immutable: adding returns a
/// new window and evicts what fell out of the span relative to the newest item. The engine starts a fresh window
/// at each kick-off, so a window never crosses the half-time boundary.
/// </summary>
public sealed class RollingWindow<T>
{
    private readonly ImmutableQueue<(int ClockMs, T Item)> _items;

    public RollingWindow(int spanMs)
        : this(spanMs, ImmutableQueue<(int ClockMs, T Item)>.Empty)
    {
    }

    private RollingWindow(int spanMs, ImmutableQueue<(int ClockMs, T Item)> items)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(spanMs);
        SpanMs = spanMs;
        _items = items;
    }

    public int SpanMs { get; }

    public bool IsEmpty => _items.IsEmpty;

    public IEnumerable<T> Items => _items.Select(i => i.Item);

    /// <summary>Appends an item at <paramref name="clockMs"/> and evicts items older than the span.</summary>
    public RollingWindow<T> Add(int clockMs, T item)
    {
        var items = _items.Enqueue((clockMs, item));
        var oldest = clockMs - SpanMs;
        while (!items.IsEmpty && items.Peek().ClockMs < oldest)
        {
            items = items.Dequeue();
        }

        return new RollingWindow<T>(SpanMs, items);
    }

    /// <summary>The items at or after <paramref name="fromClockMs"/>, oldest first.</summary>
    public IEnumerable<T> Since(int fromClockMs) => _items.Where(i => i.ClockMs >= fromClockMs).Select(i => i.Item);

    public RollingWindow<T> Clear() => new(SpanMs);
}
