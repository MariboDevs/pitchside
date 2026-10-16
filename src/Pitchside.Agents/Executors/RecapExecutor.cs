using Microsoft.Agents.AI.Workflows;
using Pitchside.Agents.Speech;
using Pitchside.Agents.Workflow;
using Pitchside.Moments;

namespace Pitchside.Agents.Executors;

/// <summary>
/// The recap stage, the workflow's output (WP-10): at full time it speaks the recap in every viewer language; for any
/// other moment, or with speech off, it hands the context on unchanged.
/// </summary>
public sealed class RecapExecutor(RecapWriter? writer) : Executor<MomentContext, MomentContext>(ExecutorId)
{
    public const string ExecutorId = "recap";

    public override async ValueTask<MomentContext> HandleAsync(MomentContext message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (writer is not { Enabled: true } || message.Moment.Type != MomentTypes.FullTime)
        {
            return message;
        }

        using var activity = AgentTelemetry.StartStage(message, ExecutorId);
        var recaps = await writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
        activity?.SetTag("pitchside.recaps", recaps.Count);
        return message with { Recaps = message.Recaps.AddRange(recaps) };
    }
}
