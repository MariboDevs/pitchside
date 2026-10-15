using Pitchside.Contracts;
using Pitchside.Moments;
using Pitchside.Stats;

namespace Pitchside.Overlay;

/// <summary>
/// The stat chips a lower third carries, at most three. A goal carries the score, a missed big chance carries xG,
/// a momentum swing carries the momentum share and shots, full time carries nothing; anything else shows its
/// first numeric evidence facts.
/// </summary>
public static class OverlayChips
{
    public const int MaxChips = 3;

    private static readonly Dictionary<string, string> MetricLabels = new(StringComparer.Ordinal)
    {
        [FactKeys.Goals] = "Goals",
        [FactKeys.Xg] = "xG",
        [FactKeys.Shots] = "Shots",
        [FactKeys.ShotsOnTarget] = "On target",
        [FactKeys.BigChances] = "Big chances",
        [FactKeys.Corners] = "Corners",
        [FactKeys.Fouls] = "Fouls",
        [FactKeys.Reds] = "Reds",
        [FactKeys.Cards] = "Cards",
        [FactKeys.Assists] = "Assists",
        [FactKeys.PossessionShare] = "Possession",
        [FactKeys.PassCompletion] = "Pass completion",
        [FactKeys.PassDifficultyAvg] = "Pass difficulty",
        [FactKeys.ProgressivePasses] = "Progressive passes",
        [FactKeys.FinalThirdEntries] = "Final third entries",
        [FactKeys.PressingIntensity] = "Pressing",
        [FactKeys.TopSpeedKph] = "Top speed",
        [FactKeys.DistanceKm] = "Distance",
        [FactKeys.Sprints] = "Sprints",
    };

    public static IReadOnlyList<OverlayChip> For(Moment moment, FactSheet sheet, MatchDocument document)
    {
        ArgumentNullException.ThrowIfNull(moment);
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(document);
        var scope = moment.SideId == document.Home.Club.Id ? FactKeys.HomeScope : FactKeys.AwayScope;
        IEnumerable<(string Label, string Key)> wanted = moment.Type switch
        {
            MomentTypes.FullTime => [],
            MomentTypes.Goal or MomentTypes.HalfTime => [(document.Home.Club.ShortName, FactKeys.MatchScoreHome), (document.Away.Club.ShortName, FactKeys.MatchScoreAway)],
            MomentTypes.BigChanceMissed => [(LabelOf(FactKeys.Xg), FactKeys.ForPlayer(moment.PlayerIds[0], FactKeys.Xg)), ("Team xG", FactKeys.ForSide(scope, FactKeys.Xg))],
            MomentTypes.MomentumSwing => [("Momentum", FactKeys.Momentum(scope)), (LabelOf(FactKeys.Shots), FactKeys.ForSide(scope, FactKeys.Shots))],
            _ => moment.Evidence.FactKeys
                .Where(k => k != FactKeys.MatchClockMinute && sheet.Find(k)?.Value.IsNumber == true)
                .Select(k => (LabelOf(k[(k.LastIndexOf('.') + 1)..]), k)),
        };

        return [.. wanted
            .Select(w => (w.Label, w.Key, Fact: sheet.Find(w.Key)))
            .Where(w => w.Fact is not null)
            .Take(MaxChips)
            .Select(w => new OverlayChip(w.Label, w.Key, FactDisplay.Format(w.Fact!)))];
    }

    private static string LabelOf(string metric) => MetricLabels.GetValueOrDefault(metric, metric);
}
