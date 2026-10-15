using Pitchside.Evaluation;

namespace Pitchside.Api.IntegrationTests;

/// <summary>A judge that scores every narrative 0.5 and counts its calls, so a test can tell a retry from a second judgement.</summary>
internal sealed class CountingJudge : IEvaluationProvider
{
    private int _calls;

    public int Calls => _calls;

    public string Name => "counting";

    public Task<double?> ScoreAsync(EvaluationItem item, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult<double?>(0.5);
    }
}
