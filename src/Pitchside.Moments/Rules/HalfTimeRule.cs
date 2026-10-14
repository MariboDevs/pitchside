using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Moments.Rules;

/// <summary>The end of the first half. Window: the whole half.</summary>
public sealed class HalfTimeRule : IMomentRule
{
    public string Type => MomentTypes.HalfTime;

    public int Priority => MomentTypes.HalfTimePriority;

    public Moment? Evaluate(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var e = context.Event;
        if (e.Type != EventType.HalfEnd || e.Period != 1)
        {
            return null;
        }

        var state = context.Current;
        var (home, away) = MomentBuilder.Score(context);
        return MomentBuilder.Create(
            context, Type, Priority, state.PeriodFirstSeq.GetValueOrDefault(1, e.Seq), null,
            [],
            state.Goals.Where(g => g.Period == 1).Select(g => g.Id).Append(e.Id),
            SummaryKeys.HalfTime,
            MomentHeadlines.HalfTime(state.ClubName(state.HomeId), home, away, state.ClubName(state.AwayId)));
    }
}
