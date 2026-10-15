using Pitchside.Contracts;
using Pitchside.Evaluation;

namespace Pitchside.Api.IntegrationTests;

/// <summary>A judge whose call for the analyst narrative times out on its own deadline; every other narrative scores 0.5.</summary>
internal sealed class TimingOutJudge : IEvaluationProvider
{
    public string Name => "timing-out";

    public Task<double?> ScoreAsync(EvaluationItem item, CancellationToken cancellationToken) =>
        item.Narrative.Persona == Personas.Analyst
            ? throw new TaskCanceledException("The judge's own request deadline passed.")
            : Task.FromResult<double?>(0.5);
}
