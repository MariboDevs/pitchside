using Pitchside.Contracts;
using Pitchside.Runtime.MomentSink;

namespace Pitchside.Agents.Tests;

/// <summary>A moment as the runtime hands it to the crew: with its sheet and the match document.</summary>
internal sealed record DetectedMoment(Moment Moment, FactSheet Sheet, MatchDocument Document)
{
    public MomentWorkItem WorkItem => new(Moment, Sheet, Document);
}
