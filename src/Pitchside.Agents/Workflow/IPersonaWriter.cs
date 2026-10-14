using Pitchside.Contracts;

namespace Pitchside.Agents.Workflow;

/// <summary>Writes persona variants of the base narrative. WP-06 implements the four viewer personas.</summary>
public interface IPersonaWriter
{
    Task<IReadOnlyList<Narrative>> WriteAsync(MomentContext context, Narrative baseNarrative, CancellationToken cancellationToken);
}
