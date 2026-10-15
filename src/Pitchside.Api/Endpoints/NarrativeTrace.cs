using Pitchside.Contracts;

namespace Pitchside.Api.Endpoints;

/// <summary>
/// The traceability chain of a narrative (WP07-R4): every sentence, every fact it cites with its value as of the
/// narrative's sheet and the events it was derived from, and those events themselves.
/// </summary>
public sealed record NarrativeTrace(
    string NarrativeId,
    int FactSheetSeq,
    IReadOnlyList<TraceSentence> Sentences,
    IReadOnlyList<MatchEvent> Events);
