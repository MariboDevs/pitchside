namespace Pitchside.Agents.Checking;

/// <summary>One reason a draft fails: which sentence, which kind of failure, and a detail for the retry prompt.</summary>
public sealed record Objection(string SentenceIndex, string Kind, string Detail);
