using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Moments.Rules;

/// <summary>A <c>goal</c> event. Window: the possession that scored, from its first event.</summary>
public sealed class GoalRule : IMomentRule
{
    public string Type => MomentTypes.Goal;

    public int Priority => MomentTypes.GoalPriority;

    public Moment? Evaluate(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var e = context.Event;
        if (e.Type != EventType.Goal)
        {
            return null;
        }

        var start = context.Current.PossessionStart ?? e;
        var shot = context.Current.Recent.Items.LastOrDefault(x => x.Type == EventType.Shot && x.Seq < e.Seq);
        var scope = context.Current.Scope(e.TeamId);
        var (home, away) = MomentBuilder.Score(context);
        List<string> keys = [FactKeys.MatchScoreHome, FactKeys.MatchScoreAway, FactKeys.ForPlayer(e.PlayerId!, FactKeys.Goals)];
        if (e.AssistId is { } assist)
        {
            keys.Add(FactKeys.ForPlayer(assist, FactKeys.Assists));
        }

        keys.AddRange([FactKeys.ForSide(scope, FactKeys.Xg), FactKeys.MatchMomentumHome, FactKeys.MatchMomentumAway]);
        return MomentBuilder.Create(
            context, Type, Priority, start.Seq, e.TeamId,
            [e.PlayerId, e.AssistId],
            new[] { start.Id, shot?.Id, e.Id }.OfType<string>(),
            keys,
            MomentHeadlines.Goal(context.Current.ClubName(e.TeamId), home, away));
    }
}
