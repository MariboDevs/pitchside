using Microsoft.Agents.AI.Workflows;
using Pitchside.Agents.Workflow;
using Pitchside.Contracts;

namespace Pitchside.Agents.Executors;

/// <summary>The persona writer stage: variants of the base narrative for each viewer persona (WP-06).</summary>
public sealed class PersonaExecutor(IPersonaWriter writer) : Executor<MomentContext, MomentContext>(ExecutorId)
{
    public const string ExecutorId = "persona-writer";

    public override async ValueTask<MomentContext> HandleAsync(MomentContext message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        using var activity = AgentTelemetry.StartStage(message, ExecutorId);
        if (message.BaseNarrative is not { Status: NarrativeStatus.Verified or NarrativeStatus.Fallback } baseNarrative)
        {
            return message;
        }

        // The base goes out before any variant is written, so the overlay never waits on a persona.
        await message.PublishAsync(baseNarrative, cancellationToken).ConfigureAwait(false);
        var variants = await writer.WriteAsync(message, baseNarrative, cancellationToken).ConfigureAwait(false);
        activity?.SetTag("pitchside.variants", variants.Count);
        return message with { Narratives = message.Narratives.AddRange(variants) };
    }
}
