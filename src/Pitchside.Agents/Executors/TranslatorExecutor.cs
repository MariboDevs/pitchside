using Microsoft.Agents.AI.Workflows;
using Pitchside.Agents.Workflow;
using Pitchside.Contracts;

namespace Pitchside.Agents.Executors;

/// <summary>The translator stage, the workflow's output: each English narrative in the viewer languages (WP-06).</summary>
public sealed class TranslatorExecutor(ITranslator translator) : Executor<MomentContext, MomentContext>(ExecutorId)
{
    public const string ExecutorId = "translator";

    public override async ValueTask<MomentContext> HandleAsync(MomentContext message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        using var activity = AgentTelemetry.StartStage(message, ExecutorId);
        var english = message.Narratives
            .Where(n => n.Language == Languages.English && n.Status is NarrativeStatus.Verified or NarrativeStatus.Fallback)
            .ToList();
        if (english.Count == 0)
        {
            return message;
        }

        var translations = await translator.TranslateAsync(message, english, cancellationToken).ConfigureAwait(false);
        activity?.SetTag("pitchside.translations", translations.Count);
        return message with { Narratives = message.Narratives.AddRange(translations) };
    }
}
