using System.Collections.Immutable;
using System.Diagnostics;
using Pitchside.Agents.Checking;
using Pitchside.Contracts;

namespace Pitchside.Agents.Workflow;

/// <summary>
/// The shared state of one moment's workflow. Every stage reads it and returns a copy with its own results
/// appended; nothing is mutated in place, so each handoff is a typed, inspectable message.
/// </summary>
public sealed record MomentContext
{
    public required Moment Moment { get; init; }
    public required FactSheet FactSheet { get; init; }
    public required MatchDocument Document { get; init; }
    public ImmutableList<NarrativeDraft> Drafts { get; init; } = [];
    public ImmutableList<Verdict> Verdicts { get; init; } = [];
    public ImmutableList<Narrative> Narratives { get; init; } = [];

    /// <summary>
    /// Where a narrative goes the moment it is final (WP06-R3): the base as soon as it is verified or composed, each
    /// persona variant and translation as it completes. Null when nothing is listening, as in the CLI and most tests.
    /// </summary>
    public Func<Narrative, CancellationToken, Task>? Publish { get; init; }

    /// <summary>The <c>pitchside.moment</c> activity every stage's activity is a child of.</summary>
    public ActivityContext Trace { get; init; }

    public int Attempts => Drafts.Count;

    public Task PublishAsync(Narrative narrative, CancellationToken cancellationToken) =>
        Publish?.Invoke(narrative, cancellationToken) ?? Task.CompletedTask;

    public NarrativeDraft? LatestDraft => Drafts.IsEmpty ? null : Drafts[^1];

    public Verdict? LatestVerdict => Verdicts.IsEmpty ? null : Verdicts[^1];

    /// <summary>The base English narrative, verified or fallback, once there is one.</summary>
    public Narrative? BaseNarrative => Narratives.FirstOrDefault(n => n.Persona == Personas.Base && n.Language == Languages.English);
}
