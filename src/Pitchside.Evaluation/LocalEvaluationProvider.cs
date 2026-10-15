using Pitchside.Contracts;

namespace Pitchside.Evaluation;

/// <summary>
/// The deterministic proxy used without Foundry, so CI has a number (WP07-R2): the share of sentences whose every
/// cited fact is among the moment's evidence. A sentence that cites nothing claims no number and counts as grounded.
/// </summary>
public sealed class LocalEvaluationProvider : IEvaluationProvider
{
    public const string ProviderName = "local";

    public string Name => ProviderName;

    public Task<double?> ScoreAsync(EvaluationItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Task.FromResult(Score(item.Narrative, item.Moment));
    }

    public static double? Score(Narrative narrative, Moment moment)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        ArgumentNullException.ThrowIfNull(moment);
        if (narrative.Sentences.Count == 0)
        {
            return null;
        }

        var evidence = moment.Evidence.FactKeys.ToHashSet(StringComparer.Ordinal);
        return (double)narrative.Sentences.Count(s => s.Citations.All(evidence.Contains)) / narrative.Sentences.Count;
    }
}
