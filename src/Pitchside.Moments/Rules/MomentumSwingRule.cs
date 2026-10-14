using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Moments.Rules;

/// <summary><c>match.momentum.{side}</c> crosses 0.70 upward. Window: the last five minutes.</summary>
public sealed class MomentumSwingRule : IMomentRule
{
    public const double Threshold = 0.70;

    public string Type => MomentTypes.MomentumSwing;

    public int Priority => MomentTypes.MomentumSwingPriority;

    public Moment? Evaluate(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var state = context.Current;
        foreach (var (teamId, previous) in new[] { (state.HomeId, context.Previous.PreviousMomentumHome), (state.AwayId, context.Previous.PreviousMomentumAway) })
        {
            var scope = state.Scope(teamId);
            var momentum = context.Sheet.Find(FactKeys.Momentum(scope));
            var now = momentum?.Value.Number ?? 0.5;
            if (previous >= Threshold || now < Threshold)
            {
                continue;
            }

            var window = momentum!.Window;
            var evidence = MomentBuilder.Since(context, context.Event.ClockMs - EventRules.FiveMinutesMs)
                .Where(e => e.TeamId == teamId && EventRules.MomentumWeight(e) > 0)
                .Select(e => e.Id)
                .DefaultIfEmpty(context.Event.Id);
            return MomentBuilder.Create(
                context, Type, Priority, window?.FromSeq ?? context.Event.Seq, teamId,
                [],
                evidence,
                [FactKeys.MatchMomentumHome, FactKeys.MatchMomentumAway, FactKeys.ForSide(scope, FactKeys.Shots),
                    FactKeys.ForSide(scope, FactKeys.FinalThirdEntries), FactKeys.ForSide(scope, FactKeys.PressingIntensity)],
                MomentHeadlines.MomentumSwing(state.ClubName(teamId)));
        }

        return null;
    }
}
