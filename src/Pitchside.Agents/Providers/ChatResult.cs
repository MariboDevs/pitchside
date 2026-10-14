namespace Pitchside.Agents.Providers;

/// <summary>What a model call returned, and what it cost.</summary>
public sealed record ChatResult(string Text, string Provider, string Model, long TokensIn, long TokensOut);
