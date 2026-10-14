using Pitchside.Contracts;
using Pitchside.Engine;

namespace Pitchside.Runtime.Tests;

/// <summary>The seed-7 match, simulated once.</summary>
internal static class Sample
{
    private static readonly Lazy<SimulatedMatch> Match = new(() => MatchSimulator.Simulate(7));

    public static MatchDocument Document => Match.Value.Document;

    public static IReadOnlyList<MatchEvent> Events => Match.Value.Events;

    public static StoredMatch Stored => new(Document, MatchStatus.Scheduled, 0, new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero));
}
