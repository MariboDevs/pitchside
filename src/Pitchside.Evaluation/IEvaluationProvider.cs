namespace Pitchside.Evaluation;

/// <summary>Scores how well a verified narrative is grounded in its facts (WP07-R2), from 0 to 1; null when it cannot say.</summary>
public interface IEvaluationProvider
{
    string Name { get; }

    Task<double?> ScoreAsync(EvaluationItem item, CancellationToken cancellationToken);
}
