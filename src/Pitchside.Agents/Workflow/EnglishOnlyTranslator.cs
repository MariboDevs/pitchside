using Pitchside.Contracts;

namespace Pitchside.Agents.Workflow;

/// <summary>The WP-04 translator stage: English only, no other languages yet.</summary>
public sealed class EnglishOnlyTranslator : ITranslator
{
    public Task<IReadOnlyList<Narrative>> TranslateAsync(MomentContext context, IReadOnlyList<Narrative> english, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Narrative>>([]);
}
