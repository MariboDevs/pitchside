using Pitchside.Agents.Workflow;
using Pitchside.Contracts;

namespace Pitchside.Agents.Tests;

/// <summary>A publisher whose first attempt fails, as a store briefly unavailable would; every later one is recorded.</summary>
public sealed class FailOncePublisher : INarrativePublisher
{
    private readonly InMemoryNarrativeLog _log = new();
    private int _attempts;

    public IReadOnlyList<Narrative> Narratives => _log.Narratives;

    public Task PublishAsync(Narrative narrative, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        return Interlocked.Increment(ref _attempts) == 1
            ? throw new InvalidOperationException("The store is briefly unavailable.")
            : _log.PublishAsync(narrative, cancellationToken);
    }
}
