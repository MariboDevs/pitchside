using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Moments.Rules;

/// <summary>A red card, straight or second yellow. Window: the foul and the card.</summary>
public sealed class RedCardRule : IMomentRule
{
    public string Type => MomentTypes.RedCard;

    public int Priority => MomentTypes.RedCardPriority;

    public Moment? Evaluate(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var e = context.Event;
        if (e.Type != EventType.Card || e.Colour != CardColour.Red || e.PlayerId is null)
        {
            return null;
        }

        var foul = context.Previous.LastFoul is { } f && f.PlayerId == e.PlayerId && f.Period == e.Period ? f : null;
        var scope = context.Current.Scope(e.TeamId);
        return MomentBuilder.Create(
            context, Type, Priority, foul?.Seq ?? e.Seq, e.TeamId,
            [e.PlayerId],
            new[] { foul?.Id, e.Id }.OfType<string>(),
            [FactKeys.ForSide(scope, FactKeys.Reds), FactKeys.ForSide(scope, FactKeys.Fouls), FactKeys.ForPlayer(e.PlayerId, FactKeys.Cards)],
            MomentHeadlines.RedCard(context.Current.PlayerName(e.PlayerId), context.Current.ClubName(e.TeamId)));
    }
}
