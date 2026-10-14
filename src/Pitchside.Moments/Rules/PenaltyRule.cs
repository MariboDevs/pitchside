using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Moments.Rules;

/// <summary>A penalty kick. Window: the foul that gave it to the shot.</summary>
public sealed class PenaltyRule : IMomentRule
{
    public string Type => MomentTypes.Penalty;

    public int Priority => MomentTypes.PenaltyPriority;

    public Moment? Evaluate(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var e = context.Event;
        if (e.Type != EventType.Shot || e.Penalty != true || e.PlayerId is null)
        {
            return null;
        }

        var foul = context.Previous.LastFoul is { } f && f.Period == e.Period && f.TeamId != e.TeamId ? f : null;
        var scope = context.Current.Scope(e.TeamId);
        return MomentBuilder.Create(
            context, Type, Priority, foul?.Seq ?? e.Seq, e.TeamId,
            [e.PlayerId],
            new[] { foul?.Id, e.Id }.OfType<string>(),
            [FactKeys.ForSide(scope, FactKeys.Xg), FactKeys.ForPlayer(e.PlayerId, FactKeys.Xg), FactKeys.MatchScoreHome, FactKeys.MatchScoreAway],
            MomentHeadlines.Penalty(context.Current.ClubName(e.TeamId)));
    }
}
