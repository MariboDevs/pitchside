using Pitchside.Agents.Workflow;
using Pitchside.Contracts;

namespace Pitchside.Agents.Tests;

/// <summary>
/// A publisher whose first attempt at a narrative the predicate picks fails, as a store briefly unavailable would;
/// every other attempt is recorded.
/// </summary>
public sealed class FailFirstMatchingPublisher(Func<Narrative, bool> fails) : INarrativePublisher
{
    private readonly InMemoryNarrativeLog _log = new();
    private int _failed;

    public IReadOnlyList<Narrative> Narratives => _log.Narratives;

    public Task PublishAsync(Narrative narrative, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        return fails(narrative) && Interlocked.CompareExchange(ref _failed, 1, 0) == 0
            ? throw new InvalidOperationException("The store is briefly unavailable.")
            : _log.PublishAsync(narrative, cancellationToken);
    }
}
