namespace Pitchside.Agents.Personalisation;

/// <summary>
/// One persona's prompt and the shape its text must have, checked in code before the fact-checker: a sentence count
/// range, and for the casual fan a word limit per sentence and a limit on how many numbers the text may carry.
/// </summary>
public sealed record PersonaSpec(string Persona, string PromptName, int MinSentences, int MaxSentences, int? MaxWords = null, int? MaxNumbers = null);
