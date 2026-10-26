using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pitchside.Agents.Rendering;
using Pitchside.Contracts;
using Pitchside.Engine;
using Pitchside.Runtime.Audit;
using Pitchside.Runtime.MomentSink;
using Pitchside.Runtime.Recaps;
using Pitchside.Runtime.Storage;

namespace Pitchside.Agents.Workflow;

/// <summary>
/// The demo loop replays one seed again and again (<c>MER-26-2</c>, <c>MER-26-3</c>), and the engine is deterministic, so
/// a moment in a later loop is the same moment as in the loop before it, on the same fact sheet. With
/// <c>Agents:ReplayEarlierLoops</c> such a moment is not written again: the narratives the crew wrote for it in the
/// loop before are published under this loop's ids, with the audit records of the model calls that wrote them and
/// any spoken recap, and no model is called. A copy keeps its provenance (the model, prompt and time it was written)
/// with <c>replay</c> added to its provider, so the control room shows it is a replay. It is used only when the loop
/// before holds a verified English base narrative for the moment and every narrative it holds renders, on this loop's
/// fact sheet, to exactly the text it has; otherwise the crew writes the moment as usual.
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

    /// <summary>A loaded loop is read again for a moment it lacks once it is this old: its last moments may have been in flight.</summary>
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
        var loaded = await LoadAsync(source, sourceMomentId, cancellationToken).ConfigureAwait(false);
        if (!loaded.Narratives.TryGetValue(sourceMomentId, out var earlier) || !CanReuse(earlier, item.FactSheet))
        {
            LogNotReused(item.Moment.Id, source);
            return false;
        }

        foreach (var record in loaded.Audit.GetValueOrDefault(sourceMomentId) ?? [])
        {
            await audit.SaveAsync(record with { MatchId = matchId, MomentId = item.Moment.Id }, cancellationToken).ConfigureAwait(false);
        }

        // The base narrative first, as the crew publishes it, then the variants and translations.
        foreach (var narrative in earlier.OrderBy(n => IsEnglishBase(n) ? 0 : 1))
        {
            await publish(Copy(narrative, source, matchId), cancellationToken).ConfigureAwait(false);
        }

        await CopyRecapsAsync(earlier, source, item.Moment, cancellationToken).ConfigureAwait(false);
        LogReused(item.Moment.Id, source, earlier.Count);
        return true;
    }

    /// <summary>Whether every earlier narrative was written at this sheet's sequence and renders on it to the same text.</summary>
    private static bool CanReuse(IReadOnlyList<Narrative> earlier, FactSheet sheet) =>
        earlier.Any(n => IsEnglishBase(n) && n.Status == NarrativeStatus.Verified)
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

    private static Narrative Copy(Narrative narrative, string from, string to) => narrative with
    {
        Id = Rekey(narrative.Id, from, to),
        MatchId = to,
        MomentId = Rekey(narrative.MomentId, from, to),
        Provenance = narrative.Provenance.Provider.EndsWith(ReplaySuffix, StringComparison.Ordinal)
            ? narrative.Provenance
            : narrative.Provenance with { Provider = narrative.Provenance.Provider + ReplaySuffix },
    };

    /// <summary>The full-time recap in each language the loop before spoke one in for this moment, under this loop's ids.</summary>
    private async Task CopyRecapsAsync(IReadOnlyList<Narrative> earlier, string source, Moment moment, CancellationToken cancellationToken)
    {
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

    /// <summary>The loop's narratives and audit records by moment, read once per loop, and again for a moment it lacked.</summary>
    private async Task<(Dictionary<string, List<Narrative>> Narratives, Dictionary<string, List<AuditRecord>> Audit)> LoadAsync(string matchId, string momentId, CancellationToken cancellationToken)
    {
        await _loading.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_loaded is { } loaded && loaded.MatchId == matchId
                && (loaded.Narratives.ContainsKey(momentId) || time.GetUtcNow() - loaded.ReadUtc < RefreshAfter))
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Moment {MomentId}: the {Language} recap could not be copied from the loop before")]
    private partial void LogRecapNotCopied(Exception exception, string momentId, string language);
}
