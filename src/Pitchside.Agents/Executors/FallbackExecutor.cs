using Microsoft.Agents.AI.Workflows;
using Pitchside.Agents.Workflow;

namespace Pitchside.Agents.Executors;

/// <summary>After the explainer's second failed attempt: the moment type's code template, verified like any draft.</summary>
public sealed class FallbackExecutor(FallbackComposer composer) : Executor<MomentContext, MomentContext>(ExecutorId)
{
    public const string ExecutorId = "fallback";

    public override ValueTask<MomentContext> HandleAsync(MomentContext message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        using var activity = AgentTelemetry.StartStage(message, ExecutorId);
        var narrative = composer.Compose(message);
        activity?.SetTag("pitchside.status", narrative.Status.ToString());
        return ValueTask.FromResult(message with { Narratives = message.Narratives.Add(narrative) });
    }
}
