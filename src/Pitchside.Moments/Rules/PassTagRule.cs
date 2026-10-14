using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Moments.Rules;

/// <summary>
/// A <c>pass.hard</c> tag whose possession goes on to a shot. Fires on the shot. Window: the pass to the shot.
/// The pass's difficulty is evidence as the pass event itself.
/// </summary>
public sealed class PassTagRule : IMomentRule
{
    public string Type => MomentTypes.PassTag;

    public int Priority => MomentTypes.PassTagPriority;

    public Moment? Evaluate(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var e = context.Event;
        if (e.Type != EventType.Shot || context.Previous.PendingHardPasses.IsEmpty)
        {
            return null;
        }

        var pass = context.Previous.PendingHardPasses.FirstOrDefault(p => p.PossessionId == e.PossessionId && p.TeamId == e.TeamId);
        if (pass is null)
        {
            return null;
        }

        var scope = context.Current.Scope(e.TeamId);
        return MomentBuilder.Create(
            context, Type, Priority, pass.Seq, e.TeamId,
            [pass.PlayerId, e.PlayerId],
            [pass.Id, e.Id],
            [FactKeys.ForSide(scope, FactKeys.ProgressivePasses), FactKeys.ForSide(scope, FactKeys.PassDifficultyAvg)],
            MomentHeadlines.HardPass(context.Current.PlayerName(pass.PlayerId)));
    }
}
