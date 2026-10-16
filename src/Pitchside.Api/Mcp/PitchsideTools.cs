using System.ComponentModel;
using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pitchside.Agents;
using Pitchside.Agents.Checking;
using Pitchside.Agents.Prompting;
using Pitchside.Agents.Providers;
using Pitchside.Agents.Workflow;
using Pitchside.Api.Broadcast;
using Pitchside.Api.Endpoints;
using Pitchside.Contracts;
using Pitchside.Runtime.Actors;
using Pitchside.Runtime.Messages;
using Pitchside.Runtime.MomentSink;
using Pitchside.Runtime.Storage;
using Pitchside.Runtime.Usage;

namespace Pitchside.Api.Mcp;

/// <summary>
/// Live match intelligence as Model Context Protocol tools (WP-08), in the same process as the match actors, so reads
/// are direct. Every tool but <c>explain_moment</c> is read-only. Who may call what, and the audit of every call, are
/// request filters (<see cref="McpServerSetup"/>), so an OAuth resource-server layer can be added without changing
/// these methods.
/// </summary>
[McpServerToolType]
public sealed class PitchsideTools(IMatchStore store, OverlayCoordinator overlay, IRequiredActor<MatchSupervisorActor> supervisor, IServiceProvider services)
{
    public const int MaxEvents = 200;

    [McpServerTool(Name = "list_matches", ReadOnly = true, Idempotent = true)]
    [Description("Lists every match the server holds. Returns one entry per match: matchId, home and away club names, status (scheduled, live, paused, completed or stopped), the last event sequence, and the score, period and clock in milliseconds from the latest fact sheet (null before kick-off).")]
    public async Task<IReadOnlyList<MatchSummary>> ListMatchesAsync(CancellationToken cancellationToken)
    {
        var summaries = new List<MatchSummary>();
        foreach (var match in await store.ListMatchesAsync(cancellationToken).ConfigureAwait(false))
        {
            var sheet = await store.GetFactSheetAsync(match.MatchId, int.MaxValue, cancellationToken).ConfigureAwait(false);
            summaries.Add(new MatchSummary(
                match.MatchId, match.Document.Home.Club.Name, match.Document.Away.Club.Name, match.Status, match.LastSeq,
                Score(sheet, "match.score.home"), Score(sheet, "match.score.away"), sheet?.Period, sheet?.ClockMs));
        }

        return summaries;
    }

    [McpServerTool(Name = "get_match", ReadOnly = true, Idempotent = true)]
    [Description("Gets one match's document: the fictional clubs, both team sheets with player ids and names, kick-off time and seed. Argument matchId is the match id, such as MER-7. Returns the match document.")]
    public async Task<MatchDocument> GetMatchAsync([Description("The match id, such as MER-7.")] string matchId, CancellationToken cancellationToken) =>
        (await MatchAsync(matchId, cancellationToken).ConfigureAwait(false)).Document;

    [McpServerTool(Name = "get_fact_sheet", ReadOnly = true, Idempotent = true)]
    [Description("Gets a match's fact sheet: every computed statistic as a keyed fact with its value, unit, precision and the event ids it derives from. Without atSeq it is the live sheet; with atSeq it is recomputed exactly as of that event sequence. Returns the fact sheet.")]
    public async Task<FactSheet> GetFactSheetAsync(
        [Description("The match id, such as MER-7.")] string matchId,
        [Description("Optional event sequence to compute the sheet as of; omit for the live sheet.")] int? atSeq = null,
        CancellationToken cancellationToken = default)
    {
        var match = await MatchAsync(matchId, cancellationToken).ConfigureAwait(false);
        if (atSeq is { } seq)
        {
            return seq < 0
                ? throw new McpException("atSeq must not be negative.")
                : await RecomputedFacts.AtAsync(store, match, seq, cancellationToken).ConfigureAwait(false);
        }

        return await LiveFactSheet.GetAsync(supervisor, store, match, cancellationToken).ConfigureAwait(false);
    }

    [McpServerTool(Name = "get_fact", ReadOnly = true, Idempotent = true)]
    [Description("Gets one fact from a match's live fact sheet with the events it was derived from (for a rolling fact, the events of the span it was computed over), so a number can be traced to what happened. Argument key is a fact key such as home.xg, match.score.away or player.AMZ-09.shots. Returns the fact, the sheet's sequence, and at most 200 events.")]
    public async Task<FactWithEvents> GetFactAsync(
        [Description("The match id, such as MER-7.")] string matchId,
        [Description("The fact key, such as home.xg or player.AMZ-09.shots.")] string key,
        CancellationToken cancellationToken)
    {
        var sheet = await GetFactSheetAsync(matchId, null, cancellationToken).ConfigureAwait(false);
        var fact = sheet.Find(key) ?? throw new McpException($"No fact '{key}' on {matchId}'s sheet.");

        // The live sheet can be ahead of the store by the events still being written; wait for them, so a score is
        // never returned without the event that made it.
        var durable = services.GetRequiredService<DurableProgress>();
        if (!await durable.WaitForAsync(matchId, sheet.AsOfSeq, Hubs.MatchSnapshots.AskTimeout, cancellationToken).ConfigureAwait(false))
        {
            throw new McpException($"{matchId}'s events up to seq {sheet.AsOfSeq} are still being stored; try again.");
        }

        // A rolling fact (rhythm, control, pressing intensity) names the span it was computed over rather than its
        // events; its evidence is that span, the latest events of it within the cap.
        var events = fact.DerivedFrom.Count == 0 && fact.Window is { } window && window.ToSeq >= window.FromSeq
            ? await store.GetEventsAsync(matchId, Math.Max(window.FromSeq, window.ToSeq - MaxEvents + 1), window.ToSeq, cancellationToken).ConfigureAwait(false)
            : await NarrativeTraces.EventsAsync(store, matchId, fact.DerivedFrom.ToHashSet(StringComparer.Ordinal), cancellationToken).ConfigureAwait(false);
        return new FactWithEvents(fact, sheet.AsOfSeq, [.. events.TakeLast(MaxEvents)]);
    }

    [McpServerTool(Name = "list_moments", ReadOnly = true, Idempotent = true)]
    [Description("Lists the moments detected in a match (goals, big chances, momentum swings, cards and more), in match order, optionally only of one type. Returns each moment with its id, type, priority, clock, headline, players and evidence.")]
    public async Task<IReadOnlyList<Moment>> ListMomentsAsync(
        [Description("The match id, such as MER-7.")] string matchId,
        [Description("Optional moment type to keep, such as goal or momentum_swing.")] string? type = null,
        CancellationToken cancellationToken = default)
    {
        await MatchAsync(matchId, cancellationToken).ConfigureAwait(false);
        var moments = await store.GetMomentsAsync(matchId, cancellationToken).ConfigureAwait(false);
        return [.. moments.Where(m => type is null || m.Type == type)];
    }

    [McpServerTool(Name = "get_narrative", ReadOnly = true, Idempotent = true)]
    [Description("Gets the narrative written about one moment for a persona and language, with its trace: every sentence, each cited fact's value and the events behind it. Persona is base, analyst, casual, club_fan or player_focus (default base); language is en, af, zu or es (default en). club_fan and player_focus have one narrative per club or player, chosen by subject (a club id such as AMZ, or a player id such as INY-07). Returns the narrative and its trace.")]
    public async Task<NarrativeWithTrace> GetNarrativeAsync(
        [Description("The match id, such as MER-7.")] string matchId,
        [Description("The moment id, such as MER-7:M:0324:goal.")] string momentId,
        [Description("Optional persona: base, analyst, casual, club_fan or player_focus.")] string? persona = null,
        [Description("Optional language: en, af, zu or es.")] string? language = null,
        [Description("For club_fan or player_focus: the club id (such as AMZ) or player id (such as INY-07) the narrative is written for.")] string? subject = null,
        CancellationToken cancellationToken = default)
    {
        var match = await MatchAsync(matchId, cancellationToken).ConfigureAwait(false);
        var wanted = persona ?? Personas.Base;
        var lang = language ?? Languages.English;
        var narratives = (await store.GetNarrativesAsync(matchId, cancellationToken).ConfigureAwait(false))
            .Where(n => n.MomentId == momentId && n.Persona == wanted && n.Language == lang && (subject is null || n.Subject == subject))
            .ToList();
        var narrative = narratives.Count switch
        {
            0 => throw new McpException($"No {wanted} narrative in '{lang}' for '{momentId}'{(subject is null ? string.Empty : $" about '{subject}'")}."),
            1 => narratives[0],
            _ => throw new McpException($"{momentId} has a {wanted} narrative for each of {string.Join(", ", narratives.Select(n => n.Subject))}; pass subject to choose one."),
        };
        return new NarrativeWithTrace(narrative, await NarrativeTraces.BuildAsync(store, match, narrative, cancellationToken).ConfigureAwait(false));
    }

    [McpServerTool(Name = "get_overlay_timeline", ReadOnly = true, Idempotent = true)]
    [Description("Gets a match's broadcast overlay timeline: each lower third with its in and out times in match milliseconds, period, state (scheduled, held, aired or expired) and stat chips. Optional fromMs keeps entries ending after that clock. Returns the entries in time order.")]
    public async Task<IReadOnlyList<OverlayEntry>> GetOverlayTimelineAsync(
        [Description("The match id, such as MER-7.")] string matchId,
        [Description("Optional clock in milliseconds; only entries that end after it are returned.")] int? fromMs = null,
        CancellationToken cancellationToken = default)
    {
        await MatchAsync(matchId, cancellationToken).ConfigureAwait(false);
        var entries = await overlay.EntriesAsync(matchId, cancellationToken).ConfigureAwait(false);
        return [.. entries.Where(e => fromMs is null || e.OutAtMs > fromMs)];
    }

    [McpServerTool(Name = "get_events", ReadOnly = true, Idempotent = true)]
    [Description("Gets a match's events between two sequence numbers, inclusive, at most 200 at a time: passes, shots, goals, fouls, cards and more, with their clock, team, player and qualifiers. Returns the events in order.")]
    public async Task<IReadOnlyList<MatchEvent>> GetEventsAsync(
        [Description("The match id, such as MER-7.")] string matchId,
        [Description("The first sequence number, at least 1.")] int fromSeq,
        [Description("The last sequence number; at most fromSeq + 199.")] int toSeq,
        CancellationToken cancellationToken)
    {
        if (fromSeq < 1 || toSeq < fromSeq || toSeq - fromSeq >= MaxEvents)
        {
            throw new McpException($"fromSeq must be at least 1, toSeq at least fromSeq, and a range at most {MaxEvents} events.");
        }

        await MatchAsync(matchId, cancellationToken).ConfigureAwait(false);
        return await store.GetEventsAsync(matchId, fromSeq, toSeq, cancellationToken).ConfigureAwait(false);
    }

    [McpServerTool(Name = "explain_moment", ReadOnly = false, Idempotent = false, OpenWorld = true)]
    [Description("Runs the agent crew's explainer and fact-checker on one moment now and returns a fresh base narrative in English, checked against the moment's fact sheet; numbers appear as [[fact.key]] tokens in sentences and rendered in renderedText. It is a model call: it counts against the server's hourly narrative limit, waits its turn under the server's cap on workflows running at once, and may need the X-Pitchside-Key header. The result is not stored.")]
    public async Task<Narrative> ExplainMomentAsync(
        [Description("The match id, such as MER-7.")] string matchId,
        [Description("The moment id, such as MER-7:M:0324:goal.")] string momentId,
        CancellationToken cancellationToken)
    {
        var match = await MatchAsync(matchId, cancellationToken).ConfigureAwait(false);
        var moment = (await store.GetMomentsAsync(matchId, cancellationToken).ConfigureAwait(false)).FirstOrDefault(m => m.Id == momentId)
            ?? throw new McpException($"No moment '{momentId}' in {matchId}.");
        var provider = services.GetService<IChatProvider>() ?? throw new McpException("This server does not run the agent crew.");
        var stored = await store.GetFactSheetAsync(matchId, moment.FactSheetSeq, cancellationToken).ConfigureAwait(false);
        var sheet = stored?.AsOfSeq == moment.FactSheetSeq ? stored : await RecomputedFacts.AtAsync(store, match, moment.FactSheetSeq, cancellationToken).ConfigureAwait(false);
        var workflow = new MomentWorkflow(
            provider,
            services.GetRequiredService<PromptCatalog>(),
            services.GetRequiredService<CodeFactChecker>(),
            new BaseOnlyPersonaWriter(),
            new EnglishOnlyTranslator(),
            services.GetRequiredService<IOptions<AgentsOptions>>(),
            services.GetRequiredService<TimeProvider>(),
            NullLogger<MomentWorkflow>.Instance);
        // The same per-host cap as the live runner: an explain_moment call waits for a slot like any moment.
        var gate = services.GetRequiredService<WorkflowGate>();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Counted once the slot is held, as the runner counts a moment: a call that waited across the hour counts in
            // the hour it runs, and one cancelled while waiting is not counted at all.
            var decision = services.GetRequiredService<UsageGuard>().TryTake(UsageLimits.Narratives, UsageGuard.HostKey);
            if (!decision.Allowed)
            {
                throw new McpException($"The hourly narrative limit is reached; try again in {(int)Math.Ceiling(decision.RetryAfter.TotalSeconds)} s.");
            }

            var context = await workflow.RunAsync(new MomentWorkItem(moment, sheet, match.Document), cancellationToken).ConfigureAwait(false);
            return context.BaseNarrative!;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<StoredMatch> MatchAsync(string matchId, CancellationToken cancellationToken) =>
        (MatchSupervisorActor.IsValidMatchId(matchId) ? await store.GetMatchAsync(matchId, cancellationToken).ConfigureAwait(false) : null)
        ?? throw new McpException($"No match '{matchId}'.");

    private static int? Score(FactSheet? sheet, string key) =>
        sheet?.Find(key)?.Value.Number is { } value ? (int)value : null;
}
