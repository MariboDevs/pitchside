using Pitchside.Agents.Workflow;
using Pitchside.Contracts;

namespace Pitchside.Agents.Tests;

/// <summary>A publisher that throws for one moment and records every other narrative.</summary>
public sealed class FailingPublisher(string failingMomentId) : INarrativePublisher
{
    private readonly InMemoryNarrativeLog _log = new();

    public IReadOnlyList<Narrative> Narratives => _log.Narratives;

    public Task PublishAsync(Narrative narrative, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        return narrative.MomentId == failingMomentId
            ? throw new InvalidOperationException("The store is unavailable.")
            : _log.PublishAsync(narrative, cancellationToken);
    }
}
