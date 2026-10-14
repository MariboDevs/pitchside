using System.Collections.Immutable;

namespace Pitchside.Stats;

/// <summary>
/// An accumulated value and the ids of the events that produced it, capped at the 50 most recent, oldest first.
/// Immutable: every operation returns a new metric and never copies more than 50 ids.
/// </summary>
public readonly record struct Metric(double Value, ImmutableArray<string> Ids)
{
    public const int MaxIds = 50;

    public static Metric Empty { get; } = new(0, []);

    /// <summary>Adds <paramref name="amount"/> and records <paramref name="eventId"/> as a source.</summary>
    public Metric Add(double amount, string eventId) => new(Value + amount, Append(Ids, eventId));

    /// <summary>Keeps the larger value; a new maximum is sourced from <paramref name="eventId"/> alone.</summary>
    public Metric Max(double candidate, string eventId) => candidate > Value ? new Metric(candidate, [eventId]) : this;

    private static ImmutableArray<string> Append(ImmutableArray<string> ids, string eventId)
    {
        if (ids.IsDefault)
        {
            return [eventId];
        }

        return ids.Length < MaxIds ? ids.Add(eventId) : ids.RemoveAt(0).Add(eventId);
    }
}
