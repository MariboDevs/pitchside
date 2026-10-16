using System.Diagnostics;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pitchside.Agents.Checking;
using Pitchside.Agents.Executors;
using Pitchside.Agents.Prompting;
using Pitchside.Agents.Providers;
using Pitchside.Agents.Speech;
using Pitchside.Contracts;
using Pitchside.Runtime.MomentSink;

namespace Pitchside.Agents.Workflow;

/// <summary>
/// One Microsoft Agent Framework workflow per moment. The graph:
/// explainer, then fact-checker; on objections back to the explainer once, then to the fallback template; on a
/// pass, or after the fallback, through the persona writer to the translator, then the recap stage, which speaks the
/// full-time recap and yields the final context.
/// </summary>
public sealed partial class MomentWorkflow(
    IChatProvider provider,
    PromptCatalog catalog,
    CodeFactChecker checker,
    IPersonaWriter personas,
    ITranslator translator,
    IOptions<AgentsOptions> options,
    TimeProvider time,
    ILogger<MomentWorkflow> logger,
    RecapWriter? recaps = null)
{
    /// <summary>The explainer runs at most twice: the first draft and one retry with the objections.</summary>
    public const int MaxExplainerAttempts = 2;

    public Microsoft.Agents.AI.Workflows.Workflow Build()
    {
        var explainer = new ExplainerExecutor(provider, catalog, time);
        var factCheck = new FactCheckExecutor(checker, provider, catalog, options.Value.AgentFactCheck);
        var fallback = new FallbackExecutor(new FallbackComposer(catalog, checker, time));
        var persona = new PersonaExecutor(personas);
        var translate = new TranslatorExecutor(translator);
        var recap = new RecapExecutor(recaps);

        return new WorkflowBuilder(explainer)
            .WithName("pitchside-moment")
            .WithDescription("Explain a moment, check every number against the fact sheet, fall back to a template, write personas and languages, speak the full-time recap.")
            .AddEdge(explainer, factCheck)
            .AddEdge<MomentContext>(factCheck, explainer, c => c is not null && !Passed(c) && c.Attempts < MaxExplainerAttempts)
            .AddEdge<MomentContext>(factCheck, fallback, c => c is not null && !Passed(c) && c.Attempts >= MaxExplainerAttempts)
            .AddEdge<MomentContext>(factCheck, persona, c => c is not null && Passed(c))
            .AddEdge(fallback, persona)
            .AddEdge(persona, translate)
            .AddEdge(translate, recap)
            .WithOutputFrom(recap)
            .Build();
    }

    /// <summary>Runs the workflow for one moment and returns its final context, never without a base narrative.</summary>
    public Task<MomentContext> RunAsync(MomentWorkItem item, CancellationToken cancellationToken) => RunAsync(item, null, cancellationToken);

    /// <summary>
    /// Runs the workflow for one moment, handing each narrative to <paramref name="publish"/> the moment it is final,
    /// and returns the final context, never without a base narrative.
    /// </summary>
    public async Task<MomentContext> RunAsync(MomentWorkItem item, Func<Narrative, CancellationToken, Task>? publish, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        using var activity = AgentTelemetry.Source.StartActivity(AgentTelemetry.MomentActivity);
        activity?.SetTag("pitchside.moment.id", item.Moment.Id);
        activity?.SetTag("pitchside.moment.type", item.Moment.Type);
        activity?.SetTag("pitchside.provider", provider.Name);

        var start = new MomentContext
        {
            Moment = item.Moment,
            FactSheet = item.FactSheet,
            Document = item.Document,
            Trace = activity?.Context ?? default,
            Publish = publish,
        };

        MomentContext? result = null;
        await using (var run = await InProcessExecution.RunAsync(Build(), start, sessionId: null, cancellationToken).ConfigureAwait(false))
        {
            foreach (var workflowEvent in run.OutgoingEvents)
            {
                switch (workflowEvent)
                {
                    case WorkflowOutputEvent output when output.As<MomentContext>() is { } context:
                        result = context;
                        break;
                    case ExecutorFailedEvent failed:
                        LogStageFailed(item.Moment.Id, failed.ExecutorId, failed.Data?.ToString());
                        break;
                    case WorkflowErrorEvent error:
                        LogStageFailed(item.Moment.Id, "workflow", error.Data?.ToString());
                        break;
                    default:
                        break;
                }
            }
        }

        // The broadcast never waits on a model and nothing uncited airs: if the workflow ended without a base
        // narrative, the template is composed here.
        if (result?.BaseNarrative is null)
        {
            var context = result ?? start;
            result = context with { Narratives = context.Narratives.Add(new FallbackComposer(catalog, checker, time).Compose(context)) };
        }

        var status = result.BaseNarrative!.Status;
        activity?.SetTag("pitchside.status", status.ToString());
        activity?.SetTag("pitchside.attempts", result.Attempts);
        activity?.SetStatus(status == NarrativeStatus.Rejected ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
        LogCompleted(item.Moment.Id, status, result.Attempts);
        return result;
    }

    private static bool Passed(MomentContext context) => context.LatestVerdict?.Passed == true;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Moment {MomentId}: stage {Stage} failed: {Detail}")]
    private partial void LogStageFailed(string momentId, string stage, string? detail);

    [LoggerMessage(Level = LogLevel.Information, Message = "Moment {MomentId}: base narrative {Status} after {Attempts} explainer attempts")]
    private partial void LogCompleted(string momentId, NarrativeStatus status, int attempts);
}
