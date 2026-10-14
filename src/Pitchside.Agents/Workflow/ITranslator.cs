using Pitchside.Contracts;

namespace Pitchside.Agents.Workflow;

/// <summary>Renders English narratives in other languages. WP-06 implements Afrikaans, isiZulu and Spanish.</summary>
public interface ITranslator
{
    Task<IReadOnlyList<Narrative>> TranslateAsync(MomentContext context, IReadOnlyList<Narrative> english, CancellationToken cancellationToken);
}
