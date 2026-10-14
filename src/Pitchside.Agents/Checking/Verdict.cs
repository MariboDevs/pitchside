namespace Pitchside.Agents.Checking;

/// <summary>The fact-checker's verdict: passed only when there are no objections.</summary>
public sealed record Verdict(IReadOnlyList<Objection> Objections)
{
    public bool Passed => Objections.Count == 0;

    public static Verdict Pass { get; } = new([]);
}
