using Pitchside.Contracts;

namespace Pitchside.Agents.Workflow;

/// <summary>Where finished narratives go: the store and the broadcaster in the runtime (WP-03), a log in tests.</summary>
public interface INarrativePublisher
{
    Task PublishAsync(Narrative narrative, CancellationToken cancellationToken);
}
