using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Moments.Rules;

/// <summary>
/// The final whistle. Window: the whole match. Beyond the side summary, the evidence names the match's top
/// players: the leading scorer (or the highest xG when nobody scored), the fastest player and the one who covered
/// the most ground.
/// </summary>
public sealed class FullTimeRule : IMomentRule
{
    public string Type => MomentTypes.FullTime;

    public int Priority => MomentTypes.FullTimePriority;

    public Moment? Evaluate(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var e = context.Event;
        if (e.Type != EventType.FullTime)
        {
            return null;
        }

        var state = context.Current;
        var (home, away) = MomentBuilder.Score(context);
        var players = context.Sheet.Facts
            .Where(f => f.Key.StartsWith(FactKeys.PlayerScope + ".", StringComparison.Ordinal))
            .Select(f => f.Key[(FactKeys.PlayerScope.Length + 1)..f.Key.LastIndexOf('.')])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        double Of(string id, string metric) => MomentBuilder.Number(context, FactKeys.ForPlayer(id, metric));

        var keys = new List<string>(SummaryKeys.FullTime);
        var top = new List<string>();
        if (players.Count > 0)
        {
            var scorer = players.OrderByDescending(id => Of(id, FactKeys.Goals)).ThenByDescending(id => Of(id, FactKeys.Xg)).First();
            var fastest = players.OrderByDescending(id => Of(id, FactKeys.TopSpeedKph)).First();
            var farthest = players.OrderByDescending(id => Of(id, FactKeys.DistanceKm)).First();
            keys.AddRange([FactKeys.ForPlayer(scorer, FactKeys.Goals), FactKeys.ForPlayer(scorer, FactKeys.Xg),
                FactKeys.ForPlayer(fastest, FactKeys.TopSpeedKph), FactKeys.ForPlayer(farthest, FactKeys.DistanceKm)]);
            top.AddRange([scorer, fastest, farthest]);
        }

        return MomentBuilder.Create(
            context, Type, Priority, state.PeriodFirstSeq.GetValueOrDefault(1, e.Seq), null,
            top,
            state.Goals.Select(g => g.Id).Append(e.Id),
            keys,
            MomentHeadlines.FullTime(state.ClubName(state.HomeId), home, away, state.ClubName(state.AwayId)));
    }
}
