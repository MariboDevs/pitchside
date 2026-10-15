using System.Globalization;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pitchside.Contracts;
using Pitchside.Runtime.Audit;

namespace Pitchside.Evaluation;

/// <summary>
/// Groundedness judged by a Foundry deployment through <c>Microsoft.Extensions.AI.Evaluation.Quality</c>'s
/// groundedness evaluator (WP07-R2): the context is the facts the narrative cites and the moment's evidence, the
/// response is the rendered text, and the evaluator's 1 to 5 score is normalised to 0 to 1. Each judgement is a model
/// call, so it writes an audit record with stage <c>groundedness</c> carrying the hash of the system prompt the evaluator
/// sent. An audit write that fails is logged and never changes the score or the judge's own failure.
/// </summary>
public sealed partial class FoundryEvaluationProvider(IChatClient judge, IAuditStore audit, TimeProvider time, ILogger<FoundryEvaluationProvider>? logger = null) : IEvaluationProvider
{
    public const string ProviderName = "foundry";
    public const string Stage = "groundedness";
    public static readonly string PromptVersion = $"groundedness-evaluator-{typeof(GroundednessEvaluator).Assembly.GetName().Version}";

    private readonly GroundednessEvaluator _evaluator = new();
    private readonly ILogger _logger = logger ?? NullLogger<FoundryEvaluationProvider>.Instance;

    public string Name => ProviderName;

    public async Task<double?> ScoreAsync(EvaluationItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        var started = time.GetTimestamp();
        var outcome = AuditOutcome.Error;
        NumericMetric? metric = null;
        var capture = new PromptCapture(judge);
        try
        {
            var question = new ChatMessage(ChatRole.User, $"Describe the {item.Moment.Type.Replace('_', ' ')} moment for a {item.Narrative.Persona.Replace('_', ' ')} viewer.");
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, item.Narrative.RenderedText));
            var result = await _evaluator.EvaluateAsync(
                [question],
                response,
                new ChatConfiguration(capture),
                [new GroundednessEvaluatorContext(Context(item))],
                cancellationToken).ConfigureAwait(false);
            metric = result.Get<NumericMetric>(GroundednessEvaluator.GroundednessMetricName);
            outcome = AuditOutcome.Ok;
            return Normalise(metric.Value);
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // The judge ran out of time (its own deadline, not the caller's cancellation): audited as a timeout.
            outcome = AuditOutcome.Timeout;
            throw;
        }
        finally
        {
            await AuditAsync(item, started, outcome, metric, capture.PromptHash).ConfigureAwait(false);
        }
    }

    /// <summary>The evaluator's 1 (poor) to 5 (excellent) as 0 to 1; null stays null.</summary>
    public static double? Normalise(double? score) => score is { } s ? Math.Clamp((s - 1) / 4, 0, 1) : null;

    /// <summary>The grounding context: each cited or evidential fact as <c>key = value unit</c>, one per line.</summary>
    public static string Context(EvaluationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var keys = item.Narrative.Sentences.SelectMany(s => s.Citations).Concat(item.Moment.Evidence.FactKeys).Distinct(StringComparer.Ordinal);
        var context = new StringBuilder();
        foreach (var fact in keys.Select(item.Sheet.Find).OfType<Fact>())
        {
            var value = fact.Value.Number is { } number
                ? number.ToString("F" + fact.Precision.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
                : fact.Value.Text;
            context.Append(CultureInfo.InvariantCulture, $"{fact.Key} = {value}{(fact.Unit is null ? string.Empty : " " + fact.Unit)}").Append('\n');
        }

        // Who the facts are about: the clubs behind home and away, and the players the moment involves or the text
        // names, so the judge can tell which club or player owns each figure.
        if (item.Document is { } document)
        {
            context.Append(CultureInfo.InvariantCulture, $"home = {document.Home.Club.Name} ({document.Home.Club.Id})").Append('\n');
            context.Append(CultureInfo.InvariantCulture, $"away = {document.Away.Club.Name} ({document.Away.Club.Id})").Append('\n');
            foreach (var side in new[] { document.Home, document.Away })
            {
                foreach (var player in side.Starters.Concat(side.Bench)
                    .Where(p => item.Moment.PlayerIds.Contains(p.Id) || item.Narrative.RenderedText.Contains(p.Name, StringComparison.Ordinal)))
                {
                    context.Append(CultureInfo.InvariantCulture, $"player {player.Id} = {player.Name}, number {player.ShirtNumber}, {side.Club.Name}").Append('\n');
                }
            }
        }

        return context.ToString();
    }

    private async Task AuditAsync(EvaluationItem item, long started, AuditOutcome outcome, NumericMetric? metric, string? promptHash)
    {
        var now = time.GetUtcNow();
        var record = new AuditRecord
        {
            Id = Ulid.New(now),
            MatchId = item.Narrative.MatchId,
            MomentId = item.Narrative.MomentId,
            Stage = Stage,
            Provider = ProviderName,
            Model = Metadata(metric, "eval-model") ?? string.Empty,
            PromptVersion = PromptVersion,
            // No call reached the judge when the hash is missing; the empty prompt's hash says so.
            PromptHash = promptHash ?? EmptyPromptHash,
            InputTokens = long.TryParse(Metadata(metric, "eval-input-tokens"), CultureInfo.InvariantCulture, out var input) ? input : 0,
            OutputTokens = long.TryParse(Metadata(metric, "eval-output-tokens"), CultureInfo.InvariantCulture, out var output) ? output : 0,
            LatencyMs = (long)time.GetElapsedTime(started).TotalMilliseconds,
            Outcome = outcome,
            RequestId = System.Diagnostics.Activity.Current?.TraceId.ToHexString() ?? string.Empty,
            Utc = now,
        };

        try
        {
            await audit.SaveAsync(record, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogAuditFailed(ex, record.MomentId);
        }
    }

    private static readonly string EmptyPromptHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData([]));

    private static string? Metadata(NumericMetric? metric, string key) =>
        metric?.Metadata is { } metadata && metadata.TryGetValue(key, out var value) ? value : null;

    [LoggerMessage(Level = LogLevel.Error, Message = "Moment {MomentId}: the groundedness audit record could not be written")]
    private partial void LogAuditFailed(Exception exception, string momentId);
}
