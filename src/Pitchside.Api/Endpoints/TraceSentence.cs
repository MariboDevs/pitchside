namespace Pitchside.Api.Endpoints;

/// <summary>One sentence of a narrative with each of its citations resolved.</summary>
public sealed record TraceSentence(string Text, IReadOnlyList<TraceCitation> Citations);
