using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pitchside.Agents.Personalisation;
using Pitchside.Agents.Rendering;
using Pitchside.Contracts;
using Pitchside.Engine;
using Pitchside.Moments;
using Pitchside.Runtime.Audit;
using Pitchside.Runtime.MomentSink;
using Pitchside.Runtime.Recaps;
using Pitchside.Runtime.Storage;

namespace Pitchside.Agents.Workflow;

/// <summary>
/// The demo loop replays one seed again and again (<c>MER-26-2</c>, <c>MER-26-3</c>), and the engine is deterministic, so
/// a moment in a later loop is the same moment as in the loop before it, on the same fact sheet. With
/// <c>Agents:ReplayEarlierLoops</c> such a moment is not written again: the narratives the crew wrote for it in the
/// loop before are published under this loop's ids, with the audit records of the model calls that wrote them (under new ids, with their own times) and
/// any spoken recap, and no model is called. A copy keeps its provenance (the model, prompt and time it was written)
/// with <c>replay</c> added to its provider, so the control room shows it is a replay, and the groundedness judge
/// leaves it alone. It is used only when the loop before holds exactly the narratives the crew would write for the
/// moment now, with a verified English base, and every one renders, on this loop's fact sheet, to exactly the text it
/// has; otherwise the crew writes the moment as usual.
/// </summary>
public sealed partial class EarlierLoopNarratives(
    IAuditStore audit,
    IRecapStore recaps,
    IOptions<AgentsOptions> options,
    TimeProvider time,
    ILogger<EarlierLoopNarratives> logger,
    IMatchStore? store = null)
{
    public const string ReplaySuffix = " replay";

    /// <summary>A loaded loop is read again for a moment it cannot be reused for once it is this old: its last moments may have been in flight.</summary>
    public static readonly TimeSpan RefreshAfter = TimeSpan.FromMinutes(1);

    private const int AuditPage = 500;

    private readonly SemaphoreSlim _loading = new(1, 1);
    private (string MatchId, DateTimeOffset ReadUtc, Dictionary<string, List<Narrative>> Narratives, Dictionary<string, List<AuditRecord>> Audit)? _loaded;

    /// <summary>
    /// Publishes the loop before's narratives for this moment, re-keyed, and returns true; or returns false, having
    /// published nothing, when this is not a later loop or the loop before cannot be reused for it.
    /// </summary>
    public async Task<bool> TryPublishAsync(MomentWorkItem item, Func<Narrative, CancellationToken, Task> publish, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(publish);
        var matchId = item.Moment.MatchId;
        if (store is null || !options.Value.ReplayEarlierLoops || MatchLoops.PreviousOf(item.Document.Seed, matchId) is not { } source)
        {
            return false;
        }

        var sourceMomentId = Rekey(item.Moment.Id, matchId, source);
        var expected = ExpectedIds(new MomentContext { Moment = item.Moment, FactSheet = item.FactSheet, Document = item.Document }, sourceMomentId);
        bool Reusable(Dictionary<string, List<Narrative>> narratives) =>
            narratives.TryGetValue(sourceMomentId, out var found) && CanReuse(found, item.FactSheet, expected);

        List<Narrative> earlier;
        try
        {
            var loaded = await LoadAsync(source, Reusable, cancellationToken).ConfigureAwait(false);
            if (!Reusable(loaded.Narratives))
            {
                LogNotReused(item.Moment.Id, source);
                return false;
            }

            earlier = loaded.Narratives[sourceMomentId];
            // A copy gets a new id, so it sorts after the records already stored for this loop, as a call made now
            // would, and a reader paging from its last id still finds it; its time and request id stay the call's own.
            var copiedAt = time.GetUtcNow();
            foreach (var (record, n) in (loaded.Audit.GetValueOrDefault(sourceMomentId) ?? []).Select((r, n) => (r, n)))
            {
                var copy = record with { Id = Ulid.New(copiedAt.AddMilliseconds(n)), MatchId = matchId, MomentId = item.Moment.Id };
                await audit.SaveAsync(copy, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // The loop before could not be read or its audit copied: nothing is published yet, so the crew writes the
            // moment, as it would without a loop before.
            LogNotPrepared(ex, item.Moment.Id, source);
            return false;
        }

        // The base narrative first, as the crew publishes it, then the variants and translations. As in the crew's run,
        // a publish that fails does not stop the rest; it is tried once more at the end, and fails the moment then.
        var failed = new List<Narrative>();
        foreach (var narrative in earlier.OrderBy(n => IsEnglishBase(n) ? 0 : 1))
        {
            var copy = Copy(narrative, source, matchId);
            try
            {
                await publish(copy, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                LogPublishFailed(ex, copy.Id);
                failed.Add(copy);
            }
        }

        foreach (var copy in failed)
        {
            await publish(copy, cancellationToken).ConfigureAwait(false);
        }

        await CopyRecapsAsync(earlier, source, item.Moment, cancellationToken).ConfigureAwait(false);
        LogReused(item.Moment.Id, source, earlier.Count);
        return true;
    }

    /// <summary>
    /// The ids the crew writes for the moment with the current options: the base and every persona variant, in English
    /// and every configured language, each one stored whether it verified, was rejected or fell back.
    /// </summary>
    private IReadOnlySet<string> ExpectedIds(MomentContext context, string sourceMomentId)
    {
        var languages = options.Value.Languages.Where(Languages.All.Contains).Prepend(Languages.English).Distinct(StringComparer.Ordinal).ToList();
        var subjects = PersonaSubjects.For(context, options.Value.Personas);
        return languages
            .SelectMany(language => subjects
                .Select(s => Narrative.IdFor(sourceMomentId, s.Spec.Persona, language, s.SubjectId))
                .Prepend(Narrative.IdFor(sourceMomentId, Personas.Base, language)))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Whether the earlier narratives are exactly the set the crew would write now, with a verified English base, each
    /// written at this sheet's sequence and rendering on it to the same text.
    /// </summary>
    private static bool CanReuse(List<Narrative> earlier, FactSheet sheet, IReadOnlySet<string> expected) =>
        expected.SetEquals(earlier.Select(n => n.Id))
        && earlier.Any(n => IsEnglishBase(n) && n.Status == NarrativeStatus.Verified)
        && earlier.All(n => n.FactSheetSeq == sheet.AsOfSeq && RendersTheSame(n, sheet));

    private static bool RendersTheSame(Narrative narrative, FactSheet sheet)
    {
        if (narrative.RenderedText.Length == 0)
        {
            return true;
        }

        try
        {
            return string.Equals(NarrativeRenderer.Render(narrative.Sentences, sheet, narrative.Language), narrative.RenderedText, StringComparison.Ordinal);
        }
        catch (InvalidOperationException)
        {
            // A cited fact is not on this sheet, so the moment is not the same one.
            return false;
        }
    }

    private static bool IsEnglishBase(Narrative narrative) =>
        narrative.Persona == Personas.Base && narrative.Subject is null && narrative.Language == Languages.English;

    /// <summary>Whether a narrative is a later loop's replay of one written before.</summary>
    public static bool IsReplay(Narrative narrative)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        return narrative.Provenance.Provider.EndsWith(ReplaySuffix, StringComparison.Ordinal);
    }

    private static Narrative Copy(Narrative narrative, string from, string to) => narrative with
    {
        Id = Rekey(narrative.Id, from, to),
        MatchId = to,
        MomentId = Rekey(narrative.MomentId, from, to),
        Provenance = IsReplay(narrative)
            ? narrative.Provenance
            : narrative.Provenance with { Provider = narrative.Provenance.Provider + ReplaySuffix },
    };

    /// <summary>The full-time recap in each language the loop before spoke one in for this moment, under this loop's ids.</summary>
    private async Task CopyRecapsAsync(IReadOnlyList<Narrative> earlier, string source, Moment moment, CancellationToken cancellationToken)
    {
        // Only full time is spoken (RecapWriter), so no other moment reads the recap store.
        if (moment.Type != MomentTypes.FullTime)
        {
            return;
        }

        var sourceMomentId = earlier[0].MomentId;
        foreach (var language in earlier.Select(n => n.Language).Prepend(Languages.English).Distinct(StringComparer.Ordinal))
        {
            try
            {
                if (await recaps.GetAsync(source, language, cancellationToken).ConfigureAwait(false) is not { } recap
                    || recap.MomentId != sourceMomentId
                    || await recaps.GetAudioAsync(source, language, cancellationToken).ConfigureAwait(false) is not { } audio)
                {
                    continue;
                }

                var copy = recap with
                {
                    MatchId = moment.MatchId,
                    MomentId = moment.Id,
                    NarrativeId = Rekey(recap.NarrativeId, source, moment.MatchId),
                    CreatedUtc = time.GetUtcNow(),
                };
                await recaps.SaveAsync(copy, audio, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // The narratives are out; a recap that cannot be copied is missing for this loop, as when speech fails.
                LogRecapNotCopied(ex, moment.Id, language);
            }
        }
    }

    /// <summary>
    /// The loop's narratives and audit records by moment, read once per loop, and read again when what is held cannot
    /// be reused for a moment (its last moments may have been in flight) and is older than <see cref="RefreshAfter"/>.
    /// </summary>
    private async Task<(Dictionary<string, List<Narrative>> Narratives, Dictionary<string, List<AuditRecord>> Audit)> LoadAsync(
        string matchId, Func<Dictionary<string, List<Narrative>>, bool> usable, CancellationToken cancellationToken)
    {
        await _loading.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_loaded is { } loaded && loaded.MatchId == matchId
                && (usable(loaded.Narratives) || time.GetUtcNow() - loaded.ReadUtc < RefreshAfter))
            {
                return (loaded.Narratives, loaded.Audit);
            }

            var narratives = await store!.GetNarrativesAsync(matchId, cancellationToken).ConfigureAwait(false);
            var records = new List<AuditRecord>();
            string? after = null;
            while (true)
            {
                var page = await audit.ListAsync(matchId, after, AuditPage, cancellationToken).ConfigureAwait(false);
                records.AddRange(page);
                if (page.Count < AuditPage)
                {
                    break;
                }

                after = page[^1].Id;
            }

            var byMoment = narratives.GroupBy(n => n.MomentId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
            var auditByMoment = records.GroupBy(r => r.MomentId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
            _loaded = (matchId, time.GetUtcNow(), byMoment, auditByMoment);
            return (byMoment, auditByMoment);
        }
        finally
        {
            _loading.Release();
        }
    }

    /// <summary>An id that starts with one match's id, such as a moment or narrative id, under another match's id.</summary>
    private static string Rekey(string id, string from, string to) =>
        id.StartsWith(from + ":", StringComparison.Ordinal)
            ? string.Concat(to, id.AsSpan(from.Length))
            : throw new InvalidOperationException($"{id} does not belong to {from}.");

    [LoggerMessage(Level = LogLevel.Information, Message = "Moment {MomentId}: {Count} narratives replayed from {Source}, no model called")]
    private partial void LogReused(string momentId, string source, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Moment {MomentId}: {Source} cannot be replayed for it, so the crew writes it")]
    private partial void LogNotReused(string momentId, string source);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Narrative {NarrativeId} could not be published as a replay; it is tried again at the end")]
    private partial void LogPublishFailed(Exception exception, string narrativeId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Moment {MomentId}: {Source} could not be read or its audit copied, so the crew writes it")]
    private partial void LogNotPrepared(Exception exception, string momentId, string source);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Moment {MomentId}: the {Language} recap could not be copied from the loop before")]
    private partial void LogRecapNotCopied(Exception exception, string momentId, string language);
}
