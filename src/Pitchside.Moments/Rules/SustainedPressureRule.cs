using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Moments.Rules;

/// <summary>
/// A side has three or more shots or two or more corners in the last three minutes without scoring in that
/// window. Evaluated on the side's shots and corners. Window: the last three minutes.
/// </summary>
public sealed class SustainedPressureRule : IMomentRule
{
    public const int WindowMs = 3 * 60_000;

    public string Type => MomentTypes.SustainedPressure;

    public int Priority => MomentTypes.SustainedPressurePriority;

    public Moment? Evaluate(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var e = context.Event;
        if (e.Type is not (EventType.Shot or EventType.Corner))
        {
            return null;
        }

        var recent = MomentBuilder.Since(context, e.ClockMs - WindowMs);
        var mine = recent.Where(x => x.TeamId == e.TeamId).ToList();
        var shots = mine.Count(x => x.Type == EventType.Shot);
        var corners = mine.Count(x => x.Type == EventType.Corner);
        if ((shots < 3 && corners < 2) || mine.Any(x => x.Type == EventType.Goal) || e.Outcome == ShotOutcome.Goal)
        {
            return null;
        }

        var scope = context.Current.Scope(e.TeamId);
        return MomentBuilder.Create(
            context, Type, Priority, recent.Count == 0 ? e.Seq : recent[0].Seq, e.TeamId,
            [],
            mine.Where(x => x.Type is EventType.Shot or EventType.Corner).Select(x => x.Id),
            [FactKeys.ForSide(scope, FactKeys.Shots), FactKeys.ForSide(scope, FactKeys.Corners),
                FactKeys.ForSide(scope, FactKeys.FinalThirdEntries), FactKeys.ForSide(scope, FactKeys.PossessionShare)],
            MomentHeadlines.SustainedPressure(context.Current.ClubName(e.TeamId)));
    }
}
