using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Pitchside.Contracts;
using Pitchside.Evaluation;
using Pitchside.Runtime.Broadcast;
using Pitchside.Runtime.Storage;

namespace Pitchside.Api.Broadcast;

/// <summary>
/// Scores verified narratives for groundedness (WP07-R2) in batches every <c>Evaluation:BatchSeconds</c>, to keep the
/// judge's cost down: each scored narrative is stored again with <c>evaluation.groundedness</c> set and pushed to its
/// watchers, so the control room shows the score. A narrative the judge cannot score keeps a null score. A storage
/// read or write that fails puts the narrative back for the next batch, keeping any score already paid for, and at
/// start the stored verified narratives that have no score yet are queued again, so a restart loses no work; a scan
/// that fails is tried again on the next tick. A narrative is judged at most once per process, however often it is
/// queued.
/// </summary>
public sealed partial class GroundednessBatcher(
    IEvaluationProvider evaluator,
    IMatchStore store,
    IMatchBroadcaster broadcaster,
    IOptions<EvaluationOptions> options,
    ILogger<GroundednessBatcher> logger) : BackgroundService
{
    private readonly Channel<Narrative> _queue = Channel.CreateUnbounded<Narrative>(new UnboundedChannelOptions { SingleReader = true });

    // Read and written only by the batch loop.
    private readonly HashSet<string> _judged = new(StringComparer.Ordinal);
    private bool _recovered;

    /// <summary>Queues a narrative for the next batch; only verified narratives are scored.</summary>
    public void Enqueue(Narrative narrative)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        if (narrative.Status == NarrativeStatus.Verified)
        {
            _queue.Writer.TryWrite(narrative);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverAsync(stoppingToken).ConfigureAwait(false);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.Value.BatchSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            if (!_recovered)
            {
                await RecoverAsync(stoppingToken).ConfigureAwait(false);
            }

            var batch = new List<Narrative>();
            while (_queue.Reader.TryRead(out var narrative))
            {
                batch.Add(narrative);
            }

            // A narrative queued twice (at start and again live, or retried) is scored once, keeping a score already paid for.
            var distinct = batch
                .GroupBy(n => n.Id, StringComparer.Ordinal)
                .Select(g => g.FirstOrDefault(n => n.Evaluation.Groundedness is not null) ?? g.First());
            foreach (var match in distinct.GroupBy(n => n.MatchId, StringComparer.Ordinal))
            {
                await ScoreAsync(match.Key, [.. match], stoppingToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Queues every stored verified narrative that has no score yet: the work a restart would otherwise drop. A scan
    /// that fails part way is run again on the next tick; what it queued twice is judged once.
    /// </summary>
    private async Task RecoverAsync(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var match in await store.ListMatchesAsync(cancellationToken).ConfigureAwait(false))
            {
                foreach (var narrative in await store.GetNarrativesAsync(match.MatchId, cancellationToken).ConfigureAwait(false))
                {
                    if (narrative.Evaluation.Groundedness is null)
                    {
                        Enqueue(narrative);
                    }
                }
            }

            _recovered = true;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogRecoveryFailed(ex);
        }
    }

    private async Task ScoreAsync(string matchId, IReadOnlyList<Narrative> narratives, CancellationToken cancellationToken)
    {
        Dictionary<string, Moment> moments;
        MatchDocument? document;
        try
        {
            moments = (await store.GetMomentsAsync(matchId, cancellationToken).ConfigureAwait(false)).ToDictionary(m => m.Id, StringComparer.Ordinal);
            document = (await store.GetMatchAsync(matchId, cancellationToken).ConfigureAwait(false))?.Document;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // No judgement has started, so the whole batch waits for the next tick rather than stopping the host.
            LogMomentsFailed(ex, matchId, narratives.Count);
            Requeue(narratives);
            return;
        }

        var sheets = new Dictionary<int, FactSheet?>();
        foreach (var narrative in narratives)
        {
            var scored = narrative;
            try
            {
                if (narrative.Evaluation.Groundedness is null)
                {
                    // Already judged in this process (queued again by the start-up scan, or live after it): no second call.
                    if (_judged.Contains(narrative.Id))
                    {
                        continue;
                    }

                    if (!sheets.TryGetValue(narrative.FactSheetSeq, out var sheet))
                    {
                        sheet = sheets[narrative.FactSheetSeq] = await store.GetFactSheetAsync(matchId, narrative.FactSheetSeq, cancellationToken).ConfigureAwait(false);
                    }

                    if (sheet is null || !moments.TryGetValue(narrative.MomentId, out var moment))
                    {
                        continue;
                    }

                    var score = await JudgeAsync(new EvaluationItem(narrative, moment, sheet, document), cancellationToken).ConfigureAwait(false);
                    _judged.Add(narrative.Id);
                    if (score is null)
                    {
                        continue;
                    }

                    scored = narrative with { Evaluation = narrative.Evaluation with { Groundedness = score } };
                }

                await store.SaveNarrativeAsync(scored, cancellationToken).ConfigureAwait(false);
                await broadcaster.NarrativeAsync(scored, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Storage failed: try again next batch, carrying the score if the judge already gave one.
                LogStorageFailed(ex, narrative.Id);
                Requeue([scored]);
            }
        }
    }

    /// <summary>The judge's score, or null when it fails: a failed judgement is logged and not retried.</summary>
    private async Task<double?> JudgeAsync(EvaluationItem item, CancellationToken cancellationToken)
    {
        try
        {
            return await evaluator.ScoreAsync(item, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogScoreFailed(ex, item.Narrative.Id);
            return null;
        }
    }

    private void Requeue(IEnumerable<Narrative> narratives)
    {
        foreach (var narrative in narratives)
        {
            _queue.Writer.TryWrite(narrative);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The stored narratives could not be read at start; the scan runs again on the next tick")]
    private partial void LogRecoveryFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Match {MatchId}: the moments could not be read, so {Count} narratives wait for the next batch")]
    private partial void LogMomentsFailed(Exception exception, string matchId, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Narrative {NarrativeId}: storage failed, so it waits for the next batch")]
    private partial void LogStorageFailed(Exception exception, string narrativeId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Narrative {NarrativeId} could not be scored for groundedness")]
    private partial void LogScoreFailed(Exception exception, string narrativeId);
}
