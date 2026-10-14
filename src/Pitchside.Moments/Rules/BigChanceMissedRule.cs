using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Moments.Rules;

/// <summary>
/// A shot from open play with xG of at least 0.30 that did not score. Window: the possession. A missed penalty
/// is already a <c>penalty</c> moment and is not counted again here.
/// </summary>
public sealed class BigChanceMissedRule : IMomentRule
{
    public string Type => MomentTypes.BigChanceMissed;

    public int Priority => MomentTypes.BigChanceMissedPriority;

    public Moment? Evaluate(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var e = context.Event;
        if (e.Type != EventType.Shot || e.Outcome == ShotOutcome.Goal || e.Penalty == true || e.Xg is not { } xg
            || xg < EventRules.BigChanceXg || e.PlayerId is null)
        {
            return null;
        }

        var start = context.Current.PossessionStart ?? e;
        var scope = context.Current.Scope(e.TeamId);
        return MomentBuilder.Create(
            context, Type, Priority, start.Seq, e.TeamId,
            [e.PlayerId],
            [start.Id, e.Id],
            [FactKeys.ForPlayer(e.PlayerId, FactKeys.Xg), FactKeys.ForSide(scope, FactKeys.Xg), FactKeys.ForSide(scope, FactKeys.BigChances),
                FactKeys.MatchScoreHome, FactKeys.MatchScoreAway],
            MomentHeadlines.BigChanceMissed(context.Current.PlayerName(e.PlayerId), context.Current.ClubName(e.TeamId)));
    }
}
