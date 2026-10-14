using Pitchside.Contracts;

namespace Pitchside.Agents.Workflow;

/// <summary>The WP-04 persona stage: the base narrative only, no variants yet.</summary>
public sealed class BaseOnlyPersonaWriter : IPersonaWriter
{
    public Task<IReadOnlyList<Narrative>> WriteAsync(MomentContext context, Narrative baseNarrative, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Narrative>>([]);
}
