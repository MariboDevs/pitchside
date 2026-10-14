namespace Pitchside.Contracts;

/// <summary>One sentence as written, with <c>[[fact.key]]</c> tokens in place of every number, and the keys it cites.</summary>
public sealed record NarrativeSentence(string Text, IReadOnlyList<string> Citations);
