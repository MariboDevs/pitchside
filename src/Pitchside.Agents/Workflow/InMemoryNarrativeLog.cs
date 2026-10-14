using System.Collections.Concurrent;
using Pitchside.Contracts;

namespace Pitchside.Agents.Workflow;

/// <summary>The local publisher: keeps every narrative in memory, in arrival order.</summary>
public sealed class InMemoryNarrativeLog : INarrativePublisher
{
    private readonly ConcurrentQueue<Narrative> _narratives = new();

    public IReadOnlyList<Narrative> Narratives => [.. _narratives];

    public Task PublishAsync(Narrative narrative, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        _narratives.Enqueue(narrative);
        return Task.CompletedTask;
    }
}
