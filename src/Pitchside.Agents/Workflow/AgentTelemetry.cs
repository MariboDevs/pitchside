using System.Diagnostics;

namespace Pitchside.Agents.Workflow;

/// <summary>
/// The crew's OpenTelemetry source. Each workflow run is one <c>pitchside.moment</c> activity with a child per
/// stage, tagged with the moment, stage, attempt, provider, model and tokens, which is what the Aspire
/// dashboard shows.
/// </summary>
public static class AgentTelemetry
{
    public const string SourceName = "Pitchside.Agents";
    public const string MomentActivity = "pitchside.moment";

    public static ActivitySource Source { get; } = new(SourceName);

    public static Activity? StartStage(MomentContext context, string stage)
    {
        ArgumentNullException.ThrowIfNull(context);
        var activity = Source.StartActivity($"pitchside.stage.{stage}", ActivityKind.Internal, context.Trace);
        activity?.SetTag("pitchside.moment.id", context.Moment.Id);
        activity?.SetTag("pitchside.stage", stage);
        activity?.SetTag("pitchside.attempts", context.Attempts);
        return activity;
    }
}
