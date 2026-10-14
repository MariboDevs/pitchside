using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Moments.Rules;

/// <summary>A <c>speed.33</c> or <c>distance.10</c> tag from the stats layer. Window: the triggering event.</summary>
public sealed class SpeedTagRule : IMomentRule
{
    public string Type => MomentTypes.SpeedTag;

    public int Priority => MomentTypes.SpeedTagPriority;

    public Moment? Evaluate(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var tag = context.Tags.FirstOrDefault(t => t.Name is TagNames.Speed33 or TagNames.Distance10);
        if (tag is null)
        {
            return null;
        }

        var speed = tag.Name == TagNames.Speed33;
        var team = context.Current.PlayerTeams.GetValueOrDefault(tag.PlayerId, context.Event.TeamId);
        var player = context.Current.PlayerName(tag.PlayerId);
        return MomentBuilder.Create(
            context, Type, Priority, context.Event.Seq, team,
            [tag.PlayerId],
            [tag.EventId],
            [FactKeys.ForPlayer(tag.PlayerId, speed ? FactKeys.TopSpeedKph : FactKeys.DistanceKm), FactKeys.ForPlayer(tag.PlayerId, FactKeys.Sprints)],
            speed ? MomentHeadlines.Speed(player) : MomentHeadlines.Distance(player));
    }
}
