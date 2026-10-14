using Pitchside.Contracts;

namespace Pitchside.Moments.Rules;

/// <summary>The opening kick-off. Its evidence is the line-ups, which live in the match document, not in facts.</summary>
public sealed class KickOffRule : IMomentRule
{
    public string Type => MomentTypes.KickOff;

    public int Priority => MomentTypes.KickOffPriority;

    public Moment? Evaluate(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var e = context.Event;
        if (e.Type != EventType.KickOff || e.Period != 1)
        {
            return null;
        }

        var state = context.Current;
        return MomentBuilder.Create(
            context, Type, Priority, e.Seq, null,
            [],
            [e.Id],
            [],
            MomentHeadlines.KickOff(state.ClubName(state.HomeId), state.ClubName(state.AwayId)));
    }
}
