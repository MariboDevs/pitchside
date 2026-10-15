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
/// watchers, so the control room shows the score. A narrative that cannot be scored keeps a null score.
/// </summary>
public sealed partial class GroundednessBatcher(
    IEvaluationProvider evaluator,
    IMatchStore store,
    IMatchBroadcaster broadcaster,
    IOptions<EvaluationOptions> options,
    ILogger<GroundednessBatcher> logger) : BackgroundService
{
    private readonly Channel<Narrative> _queue = Channel.CreateUnbounded<Narrative>(new UnboundedChannelOptions { SingleReader = true });

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
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.Value.BatchSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            var batch = new List<Narrative>();
            while (_queue.Reader.TryRead(out var narrative))
            {
                batch.Add(narrative);
            }

            foreach (var match in batch.GroupBy(n => n.MatchId, StringComparer.Ordinal))
            {
                await ScoreAsync(match.Key, [.. match], stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ScoreAsync(string matchId, IReadOnlyList<Narrative> narratives, CancellationToken cancellationToken)
    {
        var moments = (await store.GetMomentsAsync(matchId, cancellationToken).ConfigureAwait(false)).ToDictionary(m => m.Id, StringComparer.Ordinal);
        var sheets = new Dictionary<int, FactSheet?>();
        foreach (var narrative in narratives)
        {
            try
            {
                if (!sheets.TryGetValue(narrative.FactSheetSeq, out var sheet))
                {
                    sheet = sheets[narrative.FactSheetSeq] = await store.GetFactSheetAsync(matchId, narrative.FactSheetSeq, cancellationToken).ConfigureAwait(false);
                }

                if (sheet is null || !moments.TryGetValue(narrative.MomentId, out var moment))
                {
                    continue;
                }

                var score = await evaluator.ScoreAsync(new EvaluationItem(narrative, moment, sheet), cancellationToken).ConfigureAwait(false);
                if (score is null)
                {
                    continue;
                }

                var scored = narrative with { Evaluation = narrative.Evaluation with { Groundedness = score } };
                await store.SaveNarrativeAsync(scored, cancellationToken).ConfigureAwait(false);
                await broadcaster.NarrativeAsync(scored, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogScoreFailed(ex, narrative.Id);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Narrative {NarrativeId} could not be scored for groundedness")]
    private partial void LogScoreFailed(Exception exception, string narrativeId);
}
