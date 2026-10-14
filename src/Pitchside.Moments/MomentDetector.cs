using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pitchside.Contracts;
using Pitchside.Moments.Rules;
using Pitchside.Stats;

namespace Pitchside.Moments;

/// <summary>
/// Runs every rule on each event, highest priority first, and applies the rate limits. Deterministic: the same
/// events, sheets and tags always give the same moments in the same order.
/// </summary>
public sealed partial class MomentDetector : IMomentDetector
{
    private readonly IReadOnlyList<IMomentRule> _rules;
    private readonly ILogger _logger;

    public MomentDetector(ILogger<MomentDetector>? logger = null)
    {
        _logger = logger ?? NullLogger<MomentDetector>.Instance;
        _rules =
        [
            new GoalRule(), new FullTimeRule(), new RedCardRule(), new PenaltyRule(), new HalfTimeRule(),
            new BigChanceMissedRule(), new MomentumSwingRule(), new SustainedPressureRule(), new MilestoneRule(),
            new SpeedTagRule(), new PassTagRule(), new KickOffRule(),
        ];
    }

    public IReadOnlyList<IMomentRule> Rules => _rules;

    public DetectorState Start(MatchDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var names = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var teams = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var sheet in new[] { document.Home, document.Away })
        {
            foreach (var player in sheet.Starters.Concat(sheet.Bench))
            {
                names[player.Id] = player.Name;
                teams[player.Id] = sheet.Club.Id;
            }
        }

        return new DetectorState
        {
            MatchId = document.MatchId,
            HomeId = document.Home.Club.Id,
            AwayId = document.Away.Club.Id,
            ClubNames = ImmutableDictionary.CreateRange(StringComparer.Ordinal, new[]
            {
                KeyValuePair.Create(document.Home.Club.Id, document.Home.Club.Name),
                KeyValuePair.Create(document.Away.Club.Id, document.Away.Club.Name),
            }),
            PlayerNames = names.ToImmutable(),
            PlayerTeams = teams.ToImmutable(),
        };
    }

    public (DetectorState Next, IReadOnlyList<Moment> Moments) Apply(DetectorState state, MatchEvent matchEvent, FactSheet sheet, IReadOnlyList<Tag> tags)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(matchEvent);
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(tags);

        var current = Advance(state, matchEvent, tags);
        var context = new RuleContext(state, current, matchEvent, sheet, tags);
        var candidates = _rules
            .Select(rule => rule.Evaluate(context))
            .OfType<Moment>()
            .OrderByDescending(m => m.Priority)
            .ToList();

        var limiter = current.Limiter;
        var emitted = new List<Moment>(candidates.Count);
        foreach (var moment in candidates)
        {
            if (limiter.BlockedForSide(moment) || limiter.BlockedForMatch(moment))
            {
                LogDropped(moment.Id, moment.Priority);
                continue;
            }

            emitted.Add(moment);
            limiter = limiter.Record(moment);
        }

        var minute = sheet.Find(FactKeys.MatchClockMinute)?.Value.Number ?? 0;
        var next = current with
        {
            Limiter = limiter,
            PreviousMomentumHome = sheet.Find(FactKeys.MatchMomentumHome)?.Value.Number ?? 0.5,
            PreviousMomentumAway = sheet.Find(FactKeys.MatchMomentumAway)?.Value.Number ?? 0.5,
            CleanSheetChecked = state.CleanSheetChecked || (matchEvent.Period == 2 && minute >= MilestoneRule.CleanSheetMinute),
        };
        return (next, emitted);
    }

    /// <summary>Records the event in the state the rules read: the five-minute window, possession, fouls, goals.</summary>
    private static DetectorState Advance(DetectorState state, MatchEvent e, IReadOnlyList<Tag> tags)
    {
        var next = state;
        if (e.Type == EventType.KickOff)
        {
            next = next with
            {
                Period = e.Period,
                Recent = next.Recent.Clear(),
                PeriodFirstSeq = next.PeriodFirstSeq.SetItem(e.Period, e.Seq),
                PendingHardPasses = [],
            };
        }

        next = next with { Recent = next.Recent.Add(e.ClockMs, e) };

        var startsPossession = e.PossessionId > 0
            && e.Type is not (EventType.KickOff or EventType.Tracking or EventType.HalfEnd or EventType.FullTime or EventType.Substitution)
            && (next.PossessionStart is null || next.PossessionStart.PossessionId != e.PossessionId);
        if (startsPossession)
        {
            next = next with { PossessionStart = e, PendingHardPasses = [] };
        }

        switch (e.Type)
        {
            case EventType.Foul:
                next = next with { LastFoul = e };
                break;
            case EventType.Goal:
                next = next with { Goals = next.Goals.Add(e) };
                break;
            case EventType.Shot:
                next = next with { PendingHardPasses = [] };
                break;
            case EventType.Pass when tags.Any(t => t.Name == TagNames.PassHard && t.EventId == e.Id):
                next = next with { PendingHardPasses = next.PendingHardPasses.Add(e) };
                break;
            default:
                break;
        }

        return next;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Moment {MomentId} (priority {Priority}) dropped by the rate limiter")]
    private partial void LogDropped(string momentId, int priority);
}
